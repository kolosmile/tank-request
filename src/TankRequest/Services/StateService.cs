namespace TankRequest.Services
{
    using System;
    using TankRequest.Models;
    using Newtonsoft.Json;

    /// <summary>
    /// Manages loading and saving of LedgerState.
    /// </summary>
    public class StateService
    {
        private readonly Func<string, string> _getGlobal;
        private readonly Action<string, string> _setGlobal;

        public StateService(Func<string, string> getGlobal, Action<string, string> setGlobal)
        {
            _getGlobal = getGlobal;
            _setGlobal = setGlobal;
        }

        public LedgerState Load()
        {
            var json = _getGlobal("tq.state");
            if (string.IsNullOrEmpty(json))
                return new LedgerState();
            return JsonConvert.DeserializeObject<LedgerState>(json) ?? new LedgerState();
        }

        public void Save(LedgerState state)
        {
            var json = JsonConvert.SerializeObject(state);
            _setGlobal("tq.state", json);
        }

        public Config LoadConfig()
        {
            var cfg = new Config();
            
            ApplyPositiveInt(cfg, "cfg.ttlHours", value => cfg.TtlHours = value);
            ApplyPositiveInt(cfg, "cfg.bitsPerToken", value => cfg.BitsPerToken = value);
            ApplyPositiveInt(cfg, "cfg.tipPerToken", value => cfg.TipPerToken = value);
            ApplyNonNegativeInt(cfg, "cfg.tier1Tokens", value => cfg.Tier1Tokens = value);
            ApplyNonNegativeInt(cfg, "cfg.tier2Tokens", value => cfg.Tier2Tokens = value);
            ApplyNonNegativeInt(cfg, "cfg.tier3Tokens", value => cfg.Tier3Tokens = value);
            ApplyPositiveInt(cfg, "cfg.queueLines", value => cfg.QueueLines = value);
            
            var htmlPath = _getGlobal("cfg.queueHtmlPath");
            if (TryResolveConfiguredPath(htmlPath, out string resolvedHtmlPath))
                cfg.QueueHtmlPath = resolvedHtmlPath;
            else if (!string.IsNullOrWhiteSpace(htmlPath))
                AddInvalid(cfg, "cfg.queueHtmlPath");
            
            var iconPath = _getGlobal("cfg.normalIconPath");
            if (TryResolveConfiguredPath(iconPath, out string resolvedIconPath))
                cfg.NormalIconPath = resolvedIconPath;
            else if (!string.IsNullOrWhiteSpace(iconPath))
                AddInvalid(cfg, "cfg.normalIconPath");

            // Legacy overlay path is no longer consumed by the DLL, but a
            // persisted placeholder still proves that Setup UI was cancelled.
            MarkInvalidPlaceholder(cfg, "cfg.queueFile");
            
            // Reward patterns (optional - defaults work if not set)
            var supporterPattern = _getGlobal("cfg.supporterRewardPattern");
            if (IsUnresolvedValue(supporterPattern)) AddInvalid(cfg, "cfg.supporterRewardPattern");
            else if (!string.IsNullOrEmpty(supporterPattern)) cfg.SupporterRewardPattern = supporterPattern;
            
            var normalPattern = _getGlobal("cfg.normalRewardPattern");
            if (IsUnresolvedValue(normalPattern)) AddInvalid(cfg, "cfg.normalRewardPattern");
            else if (!string.IsNullOrEmpty(normalPattern)) cfg.NormalRewardPattern = normalPattern;
            
            // Hotkey patterns (optional - defaults work if not set)
            var dequeueHotkey = _getGlobal("cfg.dequeueHotkey");
            if (IsUnresolvedValue(dequeueHotkey)) AddInvalid(cfg, "cfg.dequeueHotkey");
            else if (!string.IsNullOrEmpty(dequeueHotkey)) cfg.DequeueHotkey = dequeueHotkey;
            
            var refundHotkey = _getGlobal("cfg.refundTopHotkey");
            if (IsUnresolvedValue(refundHotkey)) AddInvalid(cfg, "cfg.refundTopHotkey");
            else if (!string.IsNullOrEmpty(refundHotkey)) cfg.RefundTopHotkey = refundHotkey;

            return cfg;
        }

        private void ApplyPositiveInt(Config cfg, string key, Action<int> apply)
        {
            ApplyInt(cfg, key, value => value > 0, apply);
        }

        private void ApplyNonNegativeInt(Config cfg, string key, Action<int> apply)
        {
            ApplyInt(cfg, key, value => value >= 0, apply);
        }

        private void ApplyInt(Config cfg, string key, Func<int, bool> isValid, Action<int> apply)
        {
            string raw = _getGlobal(key);
            if (string.IsNullOrWhiteSpace(raw))
                return;

            if (IsUnresolvedValue(raw) || !int.TryParse(raw, out int value) || !isValid(value))
            {
                AddInvalid(cfg, key);
                return;
            }

            apply(value);
        }

        private void MarkInvalidPlaceholder(Config cfg, string key)
        {
            if (IsUnresolvedValue(_getGlobal(key)))
                AddInvalid(cfg, key);
        }

        private static void AddInvalid(Config cfg, string key)
        {
            if (!cfg.InvalidSettings.Contains(key))
                cfg.InvalidSettings.Add(key);
        }

        private static bool IsUnresolvedValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '%' && trimmed[trimmed.Length - 1] == '%';
        }

        /// <summary>
        /// Reject Streamer.bot placeholders that were accidentally persisted by
        /// Setup UI (for example "%cfgQueueHtml%"). Environment variables are
        /// expanded, while unresolved percent-delimited values fall back to the
        /// defaults declared in Config.
        /// </summary>
        private static bool TryResolveConfiguredPath(string value, out string resolved)
        {
            resolved = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string expanded = Environment.ExpandEnvironmentVariables(value.Trim());
            if (IsUnresolvedValue(expanded) || expanded.IndexOf('%') >= 0)
                return false;

            resolved = expanded;
            return true;
        }

        public Messages LoadMessages()
        {
            var msg = new Messages();

            // Token messages
            var v = _getGlobal("msg.tokensCredited"); if (!string.IsNullOrEmpty(v)) msg.TokensCredited = v;
            v = _getGlobal("msg.tokensAdded"); if (!string.IsNullOrEmpty(v)) msg.TokensAdded = v;
            v = _getGlobal("msg.tokensRemoved"); if (!string.IsNullOrEmpty(v)) msg.TokensRemoved = v;
            v = _getGlobal("msg.notEnoughTokens"); if (!string.IsNullOrEmpty(v)) msg.NotEnoughTokens = v;

            // TankInfo messages
            v = _getGlobal("msg.tankInfoBalance"); if (!string.IsNullOrEmpty(v)) msg.TankInfoBalance = v;
            v = _getGlobal("msg.tankInfoNoTokensInQueue"); if (!string.IsNullOrEmpty(v)) msg.TankInfoNoTokensInQueue = v;
            v = _getGlobal("msg.tankInfoEmpty"); if (!string.IsNullOrEmpty(v)) msg.TankInfoEmpty = v;

            // Queue messages
            v = _getGlobal("msg.supporterAdded"); if (!string.IsNullOrEmpty(v)) msg.SupporterAdded = v;
            v = _getGlobal("msg.artyAdded"); if (!string.IsNullOrEmpty(v)) msg.ArtyAdded = v;
            v = _getGlobal("msg.blacklistAdded"); if (!string.IsNullOrEmpty(v)) msg.BlacklistAdded = v;
            v = _getGlobal("msg.trollAdded"); if (!string.IsNullOrEmpty(v)) msg.TrollAdded = v;
            v = _getGlobal("msg.normalAdded"); if (!string.IsNullOrEmpty(v)) msg.NormalAdded = v;
            v = _getGlobal("msg.completed"); if (!string.IsNullOrEmpty(v)) msg.Completed = v;
            v = _getGlobal("msg.refundedNormal"); if (!string.IsNullOrEmpty(v)) msg.RefundedNormal = v;
            v = _getGlobal("msg.refundedAllNormal"); if (!string.IsNullOrEmpty(v)) msg.RefundedAllNormal = v;
            v = _getGlobal("msg.manualNormalAdded"); if (!string.IsNullOrEmpty(v)) msg.ManualNormalAdded = v;
            v = _getGlobal("msg.queueEmpty"); if (!string.IsNullOrEmpty(v)) msg.QueueEmpty = v;
            v = _getGlobal("msg.noNormalRequests"); if (!string.IsNullOrEmpty(v)) msg.NoNormalRequests = v;

            // Error messages
            v = _getGlobal("msg.error"); if (!string.IsNullOrEmpty(v)) msg.Error = v;
            v = _getGlobal("msg.modOnly"); if (!string.IsNullOrEmpty(v)) msg.ModOnly = v;
            v = _getGlobal("msg.userNotFound"); if (!string.IsNullOrEmpty(v)) msg.UserNotFound = v;
            v = _getGlobal("msg.targetNotEnoughTokens"); if (!string.IsNullOrEmpty(v)) msg.TargetNotEnoughTokens = v;

            // Usage messages
            v = _getGlobal("msg.usageAddTokens"); if (!string.IsNullOrEmpty(v)) msg.UsageAddTokens = v;
            v = _getGlobal("msg.usageRemoveTokens"); if (!string.IsNullOrEmpty(v)) msg.UsageRemoveTokens = v;
            v = _getGlobal("msg.usageQueueNormal"); if (!string.IsNullOrEmpty(v)) msg.UsageQueueNormal = v;
            v = _getGlobal("msg.usageQueueSupporter"); if (!string.IsNullOrEmpty(v)) msg.UsageQueueSupporter = v;

            // Help messages
            v = _getGlobal("msg.helpLine1"); if (!string.IsNullOrEmpty(v)) msg.HelpLine1 = v;
            v = _getGlobal("msg.helpLine2"); if (!string.IsNullOrEmpty(v)) msg.HelpLine2 = v;

            // Queue position messages
            v = _getGlobal("msg.queuePosActive"); if (!string.IsNullOrEmpty(v)) msg.QueuePosActive = v;
            v = _getGlobal("msg.queuePosSoon"); if (!string.IsNullOrEmpty(v)) msg.QueuePosSoon = v;
            v = _getGlobal("msg.queuePosWait"); if (!string.IsNullOrEmpty(v)) msg.QueuePosWait = v;

            return msg;
        }
    }
}
