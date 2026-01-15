namespace TankRequest.Handlers
{
    using System;
    using System.Collections.Generic;
    using TankRequest.Models;
    using TankRequest.Services;

    /// <summary>
    /// Handlers for token-related operations.
    /// </summary>
    public class TokenHandlers : BaseHandler
    {
        public TokenHandlers(
            object cph,
            Dictionary<string, object> args,
            StateService stateService,
            TokenService tokenService,
            QueueService queueService,
            OverlayService overlayService,
            Config config)
            : base(cph, args, stateService, tokenService, queueService, overlayService, config)
        {
        }

        public void HandleCreditTokens()
        {
            string eventSource = !string.IsNullOrEmpty(Arg("tipAmount")) ? "StreamElements" : "Twitch";
            
            // Try multiple tier argument names
            int tier = ArgInt("tier");
            if (tier == 0) tier = ArgInt("subTier");
            if (tier == 0) tier = ArgInt("subscriptionTier");
            
            // Normalize tier: Twitch sometimes sends 1000/2000/3000 instead of 1/2/3
            if (tier >= 1000) tier = tier / 1000;
            if (tier == 0) tier = 1;
            
            int bits = ArgInt("bits");
            decimal tipAmount = ArgDecimal("tipAmount");
            
            // Log tier for debugging
            LogInfo($"[CreditTokens] Raw tier args: tier={Arg("tier")}, subTier={Arg("subTier")}, final tier={tier}");

            // Get user info with fallbacks for StreamElements
            string tipperUserId = UserId;
            string tipperUserName = UserName;
            
            // StreamElements fallbacks: try different argument names
            if (string.IsNullOrEmpty(tipperUserName))
            {
                tipperUserName = Arg("tipUsername");
                if (string.IsNullOrEmpty(tipperUserName))
                    tipperUserName = Arg("user");
                if (string.IsNullOrEmpty(tipperUserName))
                    tipperUserName = Arg("username");
                if (string.IsNullOrEmpty(tipperUserName))
                    tipperUserName = Arg("name");
            }
            
            // If still no username, we can't credit
            if (string.IsNullOrEmpty(tipperUserName))
            {
                LogWarn($"[CreditTokens] No username found in args. Available: {string.Join(", ", _args.Keys)}");
                return;
            }
            
            // For StreamElements, use username as ID if no userId
            if (string.IsNullOrEmpty(tipperUserId))
            {
                tipperUserId = "se_" + tipperUserName.ToLower();
            }

            var state = _stateService.Load();
            
            // Check for ID migration/merge logic
            if (!string.IsNullOrEmpty(tipperUserId) && 
                !tipperUserId.StartsWith("manual_") && 
                !tipperUserId.StartsWith("se_"))
            {
                // This looks like a real ID. Check if we have an existing manual/SE user to merge.
                if (!state.users.ContainsKey(tipperUserId))
                {
                    string manualId = "manual_" + tipperUserName.ToLower();
                    string seId = "se_" + tipperUserName.ToLower();
                    
                    string existingId = null;
                    if (state.users.ContainsKey(manualId)) existingId = manualId;
                    else if (state.users.ContainsKey(seId)) existingId = seId;
                    
                    if (existingId != null)
                    {
                        // Migrate user data to real ID
                        var existingUser = state.users[existingId];
                        state.users.Remove(existingId);
                        state.users[tipperUserId] = existingUser;
                        LogInfo($"[UserMerge] Migrated {tipperUserName} from {existingId} to real ID {tipperUserId}");
                    }
                }
            }

            if (!state.users.TryGetValue(tipperUserId, out var user))
            {
                user = new UserState();
                state.users[tipperUserId] = user;
            }
            user.userName = tipperUserName;

            int tokens = _tokenService.CalculateTokens(eventSource, 
                bits > 0 ? "cheer" : (tipAmount > 0 ? "tip" : "subscription"), 
                tier, bits, tipAmount);
                
            if (tokens <= 0) return;

            _tokenService.Credit(user, tokens, "credit");
            _stateService.Save(state);

            int balance = _tokenService.GetActiveBalance(user);
            SendMessage($"@{tipperUserName}, +{tokens} támogatói token. Egyenleg: {balance}");
            LogInfo($"[CreditTokens] userId={tipperUserId} userName={tipperUserName} +{tokens} (balance={balance})");
        }

        public void HandleTankInfo()
        {
            var state = _stateService.Load();
            
            // Auto-merge logic for CALLER (Manual -> Real ID)
            if (!state.users.ContainsKey(UserId))
            {
                string manualId = "manual_" + UserName.ToLower();
                string seId = "se_" + UserName.ToLower();
                
                string existingId = null;
                if (state.users.ContainsKey(manualId)) existingId = manualId;
                else if (state.users.ContainsKey(seId)) existingId = seId;
                
                if (existingId != null)
                {
                    var existingUser = state.users[existingId];
                    state.users.Remove(existingId);
                    state.users[UserId] = existingUser;
                    LogInfo($"[TankInfoMerge] Migrated {UserName} from {existingId} to real ID {UserId}");
                }
            }

            // Check if looking up another user
            UserState user = null;
            string lookupName = UserName;
            
            var parts = RawInput.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0].StartsWith("@"))
            {
                lookupName = parts[0].TrimStart('@');
                // Find by userName
                foreach (var kvp in state.users)
                {
                    if (kvp.Value.userName != null && 
                        kvp.Value.userName.Equals(lookupName, StringComparison.OrdinalIgnoreCase))
                    {
                        user = kvp.Value;
                        lookupName = user.userName; // Correct casing
                        break;
                    }
                }
                
                if (user == null)
                {
                    SendMessage($"@{UserName}, {lookupName} nem található a rendszerben.");
                    return;
                }
            }
            else
            {
                // Look up self (caller)
                if (!state.users.TryGetValue(UserId, out user))
                {
                     SendMessage($"@{UserName}, nincs támogatói tokened.");
                     return;
                }
            }

            _tokenService.PurgeExpired(user);
            _stateService.Save(state);

            int balance = _tokenService.GetActiveBalance(user);
            var expiry = _tokenService.GetNextExpiry(user);
            var expiryStr = expiry?.ToLocalTime().ToString("MM.dd. HH:mm") ?? "-";
            
            // Queue Position Calculation
            int pos = 0;
            // Add supporter queue count
            for (int i = 0; i < state.supporterQueue.Count; i++)
            {
                if (state.supporterQueue[i].user.ToLower() == lookupName.ToLower())
                {
                    pos = i + 1;
                    break;
                }
            }
            // If not found in supporter queue, check normal queue
            if (pos == 0)
            {
                for (int i = 0; i < state.normalQueue.Count; i++)
                {
                    if (state.normalQueue[i].user.ToLower() == lookupName.ToLower())
                    {
                        pos = state.supporterQueue.Count + i + 1;
                        break;
                    }
                }
            }
            
            string queueInfo = "";
            if (pos > 0)
            {
                // ETA Calculation: (Position - 2) * BattleDuration
                // Position 1 means active battle. Position 2 is next.
                // If I am at Pos 3, I wait for Pos 1 (active) and Pos 2 (next).
                if (pos == 1)
                {
                    queueInfo = $" Pozíció: {pos}. (Épp csatában.)";
                }
                else
                {
                    int waitCount = pos - 2;
                    if (waitCount <= 0) // Pos 2 basically
                    {
                        queueInfo = $" Pozíció: {pos}. (Hamarosan sorra kerülsz.)";
                    }
                    else
                    {
                        int totalMinutes = waitCount * _config.BattleDurationMinutes;
                        string eta = totalMinutes < 60 
                            ? $"{totalMinutes} perc" 
                            : $"{totalMinutes / 60} óra {totalMinutes % 60} perc";
                        queueInfo = $" Pozíció: {pos}. (kb. {eta} múlva)";
                    }
                }
            }

            string targetPrefix = lookupName.Equals(UserName, StringComparison.OrdinalIgnoreCase) 
                ? "" 
                : $"{lookupName} ";

            SendMessage($"@{UserName}, {targetPrefix}Egyenleg: {balance} (lejár: {expiryStr}).{queueInfo}");
        }

        public void HandleTankHelp()
        {
            SendMessage($" Így kérhetsz tankot: 1. Normál: Csatornapontból. 2. Támogatói: Tokenekkel (⭐prioritás). 🪙Token jár: Sub (T1={_config.Tier1Tokens}tk, T2={_config.Tier2Tokens}tk, T3={_config.Tier3Tokens}tk), Cheer ({_config.BitsPerToken}b=1tk), Tip ({_config.TipPerToken}€=1tk).");
            SendMessage($"🕒A tokenek {_config.TtlHours} óráig érvényesek! ⚠️Speciális: xA (Arty, {_config.CostArty}tk), xB (Blacklist, {_config.CostBlacklist}tk), xT (Troll, {_config.CostTroll}tk). 📈Többszörös Bombardino pontért használj szorzót (pl. Tiger x3). Egyenleg: !tankinfo");
        }

        public void HandleAddTokens()
        {
            if (!IsModOrBroadcaster)
            {
                SendMessage($"@{UserName}, csak mod/broadcaster használhatja ezt a parancsot.");
                return;
            }
            
            var parts = RawInput.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            string targetUserId;
            string targetUserName;
            int amount;
            
            if (parts.Length == 1 && int.TryParse(parts[0], out amount))
            {
                targetUserId = UserId;
                targetUserName = UserName;
            }
            else if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1], out amount))
            {
                targetUserName = parts[0].TrimStart('@');
                // Lookup by userName (will be done after state load)
                targetUserId = null;
            }
            else
            {
                SendMessage($"@{UserName}, használat: !addtokens [mennyiség] vagy !addtokens [felhasználó] [mennyiség]");
                return;
            }
            
            var state = _stateService.Load();
            UserState user = null;
            
            if (targetUserId != null)
            {
                // Self - use direct ID lookup
                if (!state.users.TryGetValue(targetUserId, out user))
                {
                    user = new UserState();
                    state.users[targetUserId] = user;
                }
            }
            else
            {
                // Target user - find by userName
                foreach (var kvp in state.users)
                {
                    if (kvp.Value.userName != null && 
                        kvp.Value.userName.Equals(targetUserName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetUserId = kvp.Key;
                        user = kvp.Value;
                        targetUserName = user.userName; // Use proper casing
                        break;
                    }
                }
                // If user not found, create new with manual_ prefix
                if (user == null)
                {
                    targetUserId = "manual_" + targetUserName.ToLower();
                    user = new UserState();
                    state.users[targetUserId] = user;
                }
            }
            user.userName = targetUserName;
            
            _tokenService.Credit(user, amount, "manual");
            _stateService.Save(state);
            
            int balance = _tokenService.GetActiveBalance(user);
            SendMessage($"@{targetUserName}, +{amount} token. Egyenleg: {balance}");
            LogInfo($"[AddTokens] {targetUserName} +{amount} (balance={balance})");
        }

        public void HandleRemoveTokens()
        {
            if (!IsModOrBroadcaster)
            {
                SendMessage($"@{UserName}, csak mod/broadcaster használhatja ezt a parancsot.");
                return;
            }
            
            var parts = RawInput.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            string targetUserId;
            string targetUserName;
            int amount;
            
            if (parts.Length == 1 && int.TryParse(parts[0], out amount))
            {
                targetUserId = UserId;
                targetUserName = UserName;
            }
            else if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1], out amount))
            {
                targetUserName = parts[0].TrimStart('@');
                targetUserId = null;
            }
            else
            {
                SendMessage($"@{UserName}, használat: !removetokens [mennyiség] vagy !removetokens [felhasználó] [mennyiség]");
                return;
            }
            
            var state = _stateService.Load();
            UserState user = null;
            
            if (targetUserId != null)
            {
                state.users.TryGetValue(targetUserId, out user);
            }
            else
            {
                foreach (var kvp in state.users)
                {
                    if (kvp.Value.userName != null && 
                        kvp.Value.userName.Equals(targetUserName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetUserId = kvp.Key;
                        user = kvp.Value;
                        targetUserName = user.userName;
                        break;
                    }
                }
            }
            
            if (user == null)
            {
                SendMessage($"@{UserName}, {targetUserName} nem található.");
                return;
            }
            
            int removed = _tokenService.Remove(user, amount);
            _tokenService.PurgeExpired(user);
            _stateService.Save(state);
            
            int balance = _tokenService.GetActiveBalance(user);
            SendMessage($"@{targetUserName}, -{removed} token. Egyenleg: {balance}");
            LogInfo($"[RemoveTokens] {targetUserName} -{removed} (balance={balance})");
        }
    }
}
