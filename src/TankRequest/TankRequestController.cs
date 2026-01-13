namespace TankRequest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TankRequest.Models;
    using TankRequest.Services;
    using TankRequest.Handlers;

    /// <summary>
    /// Main controller that handles all tank request operations.
    /// Routes actions to appropriate handler classes.
    /// </summary>
    public class TankRequestController
    {
        private readonly dynamic _cph;
        private readonly Dictionary<string, object> _args;
        private readonly StateService _stateService;
        private readonly TokenService _tokenService;
        private readonly QueueService _queueService;
        private readonly OverlayService _overlayService;
        private readonly Config _config;

        private readonly TokenHandlers _tokenHandlers;
        private readonly QueueHandlers _queueHandlers;

        public TankRequestController(dynamic cph, Dictionary<string, object> args)
        {
            _cph = cph;
            _args = args;
            
            _stateService = new StateService(
                key => GetGlobal(key),
                (key, value) => SetGlobal(key, value)
            );
            _config = _stateService.LoadConfig();
            _tokenService = new TokenService(_config);
            _queueService = new QueueService(_config);
            _overlayService = new OverlayService(_config);

            // Initialize handlers (cast dynamic to object to avoid constructor initializer error)
            _tokenHandlers = new TokenHandlers((object)cph, args, _stateService, _tokenService, _queueService, _overlayService, _config);
            _queueHandlers = new QueueHandlers((object)cph, args, _stateService, _tokenService, _queueService, _overlayService, _config);
        }

        #region Helper Methods

        private string GetGlobal(string key) => _cph.GetGlobalVar<string>(key, true) ?? "";
        private void SetGlobal(string key, string value) => _cph.SetGlobalVar(key, value, true);
        private void LogInfo(string msg) => _cph.LogInfo(msg);
        private void LogWarn(string msg) => _cph.LogWarn(msg);
        
        private string Arg(string key) => _args.ContainsKey(key) ? _args[key]?.ToString() ?? "" : "";

        /// <summary>
        /// Cleanup expired tokens from all users.
        /// </summary>
        private void CleanupAllExpiredTokens()
        {
            var state = _stateService.Load();
            bool changed = false;
            
            foreach (var user in state.users.Values)
            {
                int before = user.buckets.Count;
                _tokenService.PurgeExpired(user);
                if (user.buckets.Count != before)
                    changed = true;
            }
            
            // Remove users with no buckets
            var emptyUsers = state.users.Where(kv => kv.Value.buckets.Count == 0).Select(kv => kv.Key).ToList();
            foreach (var userId in emptyUsers)
            {
                state.users.Remove(userId);
                changed = true;
            }
            
            if (changed)
            {
                _stateService.Save(state);
                LogInfo("[TankRequest] Expired tokens cleaned up.");
            }
        }

        #endregion

        #region Main Entry Point

        /// <summary>
        /// Main entry point - auto-detects action from args and routes to handler.
        /// </summary>
        public bool Execute()
        {
            // Cleanup all expired tokens on every call
            CleanupAllExpiredTokens();
            
            // Debug: log all args keys
            LogInfo($"[TankRequest] Args keys: {string.Join(", ", _args.Keys)}");
            
            string action = DetectAction();
            LogInfo($"[TankRequest] Detected action: {action}");

            switch (action)
            {
                // Token handlers
                case "credit_tokens": _tokenHandlers.HandleCreditTokens(); break;
                case "balance": _tokenHandlers.HandleBalance(); break;
                case "add_tokens": _tokenHandlers.HandleAddTokens(); break;
                case "remove_tokens": _tokenHandlers.HandleRemoveTokens(); break;
                
                // Queue handlers
                case "supporter_redeem": _queueHandlers.HandleSupporterRedeem(); break;
                case "normal_redeem": _queueHandlers.HandleNormalRedeem(); break;
                case "dequeue": _queueHandlers.HandleDequeue(); break;
                case "refund_top": _queueHandlers.HandleRefundTop(); break;
                case "refund_all": _queueHandlers.HandleRefundAllNormal(); break;
                case "render_queue": _queueHandlers.HandleRenderQueue(); break;
                case "queue_normal": _queueHandlers.HandleQueueNormal(); break;
                case "queue_supporter": _queueHandlers.HandleQueueSupporter(); break;
                
                default:
                    LogWarn($"[TankRequest] Unknown action: {action}");
                    break;
            }

            return true;
        }

        private string DetectAction()
        {
            string command = Arg("command");
            string rewardName = Arg("rewardName");
            
            // Command triggers
            if (!string.IsNullOrEmpty(command))
            {
                if (command.ToLower() == "!tank") return "balance";
                if (command.ToLower() == "!refund") return "refund_all";
                if (command.ToLower() == "!addtokens") return "add_tokens";
                if (command.ToLower() == "!removetokens") return "remove_tokens";
                if (command.ToLower() == "!queuenormal") return "queue_normal";
                if (command.ToLower() == "!queuesupporter") return "queue_supporter";
            }
            
            // Channel Point triggers - uses config patterns
            if (!string.IsNullOrEmpty(rewardName))
            {
                var lowerName = rewardName.ToLower();
                if (lowerName.Contains(_config.SupporterRewardPattern.ToLower())) 
                    return "supporter_redeem";
                if (lowerName.Contains(_config.NormalRewardPattern.ToLower())) 
                    return "normal_redeem";
            }
            
            // Sub/Cheer/Tip triggers
            if (!string.IsNullOrEmpty(Arg("tier")) || !string.IsNullOrEmpty(Arg("monthsSubscribed")))
                return "credit_tokens";
            if (!string.IsNullOrEmpty(Arg("bits")))
                return "credit_tokens";
            if (!string.IsNullOrEmpty(Arg("tipAmount")))
                return "credit_tokens";
            
            // Hotkey triggers - build combo from key + modifiers
            string key = Arg("key");
            if (!string.IsNullOrEmpty(key))
            {
                var parts = new List<string>();
                if (Arg("hasShift") == "True") parts.Add("Shift");
                if (Arg("hasAlt") == "True") parts.Add("Alt");
                if (Arg("hasCtrl") == "True") parts.Add("Ctrl");
                parts.Add(key);
                string pressedCombo = string.Join("+", parts).ToLower();
                
                string dequeueHotkey = _config.DequeueHotkey.ToLower();
                string refundHotkey = _config.RefundTopHotkey.ToLower();
                
                LogInfo($"[TankRequest] Hotkey: pressed={pressedCombo}, dequeue={dequeueHotkey}, refund={refundHotkey}");
                
                if (pressedCombo == dequeueHotkey) return "dequeue";
                if (pressedCombo == refundHotkey) return "refund_top";
            }
                
            // Fallback: manually set action
            return Arg("action");
        }

        #endregion
    }
}
