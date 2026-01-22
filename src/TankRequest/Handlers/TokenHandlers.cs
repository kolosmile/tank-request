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
            Config config,
            Messages messages)
            : base(cph, args, stateService, tokenService, queueService, overlayService, config, messages)
        {
        }

        public void HandleCreditTokens()
        {
            string eventSource = !string.IsNullOrEmpty(Arg("tipAmount")) ? "StreamElements" : "Twitch";
            
            // Try multiple tier argument names and formats
            int tier = ParseTier(Arg("tier"));
            if (tier == 0) tier = ParseTier(Arg("subTier"));
            if (tier == 0) tier = ParseTier(Arg("subscriptionTier"));
            
            // Normalize tier: Twitch sometimes sends 1000/2000/3000 instead of 1/2/3
            if (tier >= 1000) tier = tier / 1000;
            if (tier == 0) tier = 1;
            
            int bits = ArgInt("bits");
            decimal tipAmount = ArgDecimal("tipAmount");
            int giftCount = ArgInt("gifts");
            if (giftCount == 0) giftCount = 1;
            
            // Log tier for debugging
            LogInfo($"[CreditTokens] Raw tier args: tier={Arg("tier")}, subTier={Arg("subTier")}, final tier={tier}, gifts={giftCount}");

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

            // Determine event type based on arguments
            string eventType = "subscription";
            if (bits > 0) eventType = "cheer";
            else if (tipAmount > 0) eventType = "tip";
            else if (giftCount > 1) eventType = "giftbomb";

            int tokens = _tokenService.CalculateTokens(eventSource, eventType, tier, bits, tipAmount, giftCount);
                
            if (tokens <= 0) return;

            _tokenService.Credit(user, tokens, "credit");
            _stateService.Save(state);

            int balance = _tokenService.GetActiveBalance(user);
            SendMessage(Msg(_msg.TokensCredited, ("user", tipperUserName), ("amount", tokens), ("balance", balance)));
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
            bool userHasTokens = true;
            
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
                    // User not in token system, but might still be in queue
                    userHasTokens = false;
                }
            }
            else
            {
                // Look up self (caller)
                if (!state.users.TryGetValue(UserId, out user))
                {
                    userHasTokens = false;
                }
            }

            int balance = 0;
            string expiryStr = "-";
            
            if (user != null)
            {
                _tokenService.PurgeExpired(user);
                _stateService.Save(state);
                balance = _tokenService.GetActiveBalance(user);
                var expiry = _tokenService.GetNextExpiry(user);
                expiryStr = expiry?.ToLocalTime().ToString("MM.dd. HH:mm") ?? "-";
            }
            
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
                    queueInfo = Msg(_msg.QueuePosActive, ("pos", pos));
                }
                else
                {
                    int waitCount = pos - 2;
                    if (waitCount <= 0) // Pos 2 basically
                    {
                        queueInfo = Msg(_msg.QueuePosSoon, ("pos", pos));
                    }
                    else
                    {
                        int totalMinutes = waitCount * _config.BattleDurationMinutes;
                        string eta = totalMinutes < 60 
                            ? $"{totalMinutes} perc" 
                            : $"{totalMinutes / 60} óra {totalMinutes % 60} perc";
                        queueInfo = Msg(_msg.QueuePosWait, ("pos", pos), ("eta", eta));
                    }
                }
            }

            string targetPrefix = lookupName.Equals(UserName, StringComparison.OrdinalIgnoreCase) 
                ? "" 
                : $"{lookupName} ";

            // Build response based on what info we have
            if (balance > 0)
            {
                SendMessage(Msg(_msg.TankInfoBalance, ("user", UserName), ("target", targetPrefix), ("balance", balance), ("expiry", expiryStr), ("queueInfo", queueInfo)));
            }
            else if (pos > 0)
            {
                SendMessage(Msg(_msg.TankInfoNoTokensInQueue, ("user", UserName), ("target", targetPrefix), ("queueInfo", queueInfo)));
            }
            else
            {
                SendMessage(Msg(_msg.TankInfoEmpty, ("user", UserName), ("target", targetPrefix)));
            }
        }

        public void HandleTankHelp()
        {
            SendMessage(Msg(_msg.HelpLine1, ("tier1", _config.Tier1Tokens), ("tier2", _config.Tier2Tokens), ("tier3", _config.Tier3Tokens), ("bitsPerToken", _config.BitsPerToken), ("tipPerToken", _config.TipPerToken)));
            SendMessage(Msg(_msg.HelpLine2, ("ttlHours", _config.TtlHours), ("costArty", _config.CostArty), ("costBlacklist", _config.CostBlacklist), ("costTroll", _config.CostTroll)));
        }

        public void HandleAddTokens()
        {
            if (!IsModOrBroadcaster)
            {
                SendMessage(Msg(_msg.ModOnly, ("user", UserName)));
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
                SendMessage(Msg(_msg.UsageAddTokens, ("user", UserName)));
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
            SendMessage(Msg(_msg.TokensAdded, ("user", targetUserName), ("amount", amount), ("balance", balance)));
            LogInfo($"[AddTokens] {targetUserName} +{amount} (balance={balance})");
        }

        public void HandleRemoveTokens()
        {
            if (!IsModOrBroadcaster)
            {
                SendMessage(Msg(_msg.ModOnly, ("user", UserName)));
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
                SendMessage(Msg(_msg.UsageRemoveTokens, ("user", UserName)));
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
                SendMessage(Msg(_msg.UserNotFound, ("user", UserName), ("target", targetUserName)));
                return;
            }
            
            int removed = _tokenService.Remove(user, amount);
            _tokenService.PurgeExpired(user);
            _stateService.Save(state);
            
            int balance = _tokenService.GetActiveBalance(user);
            SendMessage(Msg(_msg.TokensRemoved, ("user", targetUserName), ("amount", removed), ("balance", balance)));
            LogInfo($"[RemoveTokens] {targetUserName} -{removed} (balance={balance})");
        }

        /// <summary>
        /// Parse tier from various formats: "1", "1000", "tier 1", "Tier 1", etc.
        /// </summary>
        private int ParseTier(string tierValue)
        {
            if (string.IsNullOrEmpty(tierValue)) return 0;
            
            // Try direct numeric parse first
            if (int.TryParse(tierValue, out int numericTier))
                return numericTier;
            
            // Handle "tier X" format (case insensitive)
            tierValue = tierValue.ToLower().Trim();
            if (tierValue.StartsWith("tier "))
            {
                string numPart = tierValue.Substring(5).Trim();
                if (int.TryParse(numPart, out int tier))
                    return tier;
            }
            
            // Handle "prime" as tier 1
            if (tierValue.Contains("prime"))
                return 1;
            
            return 0;
        }
    }
}
