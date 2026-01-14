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
            
            int tier = ArgInt("tier");
            if (tier == 0) tier = 1;
            int bits = ArgInt("bits");
            decimal tipAmount = ArgDecimal("tipAmount");

            var state = _stateService.Load();
            if (!state.users.TryGetValue(UserId, out var user))
            {
                user = new UserState();
                state.users[UserId] = user;
            }
            user.userName = UserName;

            int tokens = _tokenService.CalculateTokens(eventSource, 
                bits > 0 ? "cheer" : (tipAmount > 0 ? "tip" : "subscription"), 
                tier, bits, tipAmount);
                
            if (tokens <= 0) return;

            _tokenService.Credit(user, tokens, "credit");
            _stateService.Save(state);

            int balance = _tokenService.GetActiveBalance(user);
            SendMessage($"@{UserName}, +{tokens} támogatói token. Egyenleg: {balance}");
            LogInfo($"[CreditTokens] userId={UserId} +{tokens} (balance={balance})");
        }

        public void HandleTankInfo()
        {
            var state = _stateService.Load();
            if (!state.users.TryGetValue(UserId, out var user))
            {
                // Ha nincs user state, csak üzenjük meg
                 SendMessage($"@{UserName}, nincs támogatói tokened.");
                 return;
            }

            _tokenService.PurgeExpired(user);
            _stateService.Save(state);

            int balance = _tokenService.GetActiveBalance(user);
            var expiry = _tokenService.GetNextExpiry(user);
            var expiryStr = expiry?.ToLocalTime().ToString("HH:mm") ?? "-";
            
            // Queue Position Calculation
            int pos = 0;
            // Add supporter queue count
            for (int i = 0; i < state.supporterQueue.Count; i++)
            {
                if (state.supporterQueue[i].user.ToLower() == UserName.ToLower())
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
                    if (state.normalQueue[i].user.ToLower() == UserName.ToLower())
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

            SendMessage($"@{UserName}, Egyenleg: {balance} (lejár: {expiryStr}).{queueInfo}");
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
                if (user == null)
                {
                    SendMessage($"@{UserName}, {targetUserName} nem található a rendszerben.");
                    return;
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
