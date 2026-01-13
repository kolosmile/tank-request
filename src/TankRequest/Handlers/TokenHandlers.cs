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

        public void HandleBalance()
        {
            var state = _stateService.Load();
            if (!state.users.TryGetValue(UserId, out var user))
            {
                SendMessage($"@{UserName}, nincs támogatói tokened.");
                return;
            }

            _tokenService.PurgeExpired(user);
            _stateService.Save(state);

            int balance = _tokenService.GetActiveBalance(user);
            if (balance == 0)
            {
                SendMessage($"@{UserName}, nincs támogatói tokened.");
                return;
            }

            var expiry = _tokenService.GetNextExpiry(user);
            var expiryStr = expiry?.ToLocalTime().ToString("MM-dd HH:mm") ?? "";
            SendMessage($"@{UserName}, elérhető tokenek: {balance} (lejár: {expiryStr})");
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
                targetUserId = targetUserName.ToLower();
            }
            else
            {
                SendMessage($"@{UserName}, használat: !addtokens [mennyiség] vagy !addtokens [felhasználó] [mennyiség]");
                return;
            }
            
            var state = _stateService.Load();
            if (!state.users.TryGetValue(targetUserId, out var user))
            {
                user = new UserState();
                state.users[targetUserId] = user;
            }
            user.userName = targetUserName;
            
            _tokenService.Credit(user, amount, "test");
            _stateService.Save(state);
            
            int balance = _tokenService.GetActiveBalance(user);
            SendMessage($"@{targetUserName}, +{amount} teszt token. Egyenleg: {balance}");
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
                targetUserId = targetUserName.ToLower();
            }
            else
            {
                SendMessage($"@{UserName}, használat: !removetokens [mennyiség] vagy !removetokens [felhasználó] [mennyiség]");
                return;
            }
            
            var state = _stateService.Load();
            if (!state.users.TryGetValue(targetUserId, out var user))
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
