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
            
            if (int.TryParse(_getGlobal("cfg.ttlHours"), out int ttl)) cfg.TtlHours = ttl;
            if (int.TryParse(_getGlobal("cfg.bitsPerToken"), out int bpt)) cfg.BitsPerToken = bpt;
            if (int.TryParse(_getGlobal("cfg.tipPerToken"), out int tpt)) cfg.TipPerToken = tpt;
            if (int.TryParse(_getGlobal("cfg.tier1Tokens"), out int t1)) cfg.Tier1Tokens = t1;
            if (int.TryParse(_getGlobal("cfg.tier2Tokens"), out int t2)) cfg.Tier2Tokens = t2;
            if (int.TryParse(_getGlobal("cfg.tier3Tokens"), out int t3)) cfg.Tier3Tokens = t3;
            if (int.TryParse(_getGlobal("cfg.queueLines"), out int ql)) cfg.QueueLines = ql;
            
            var htmlPath = _getGlobal("cfg.queueHtmlPath");
            if (!string.IsNullOrEmpty(htmlPath)) cfg.QueueHtmlPath = htmlPath;
            
            var iconPath = _getGlobal("cfg.normalIconPath");
            if (!string.IsNullOrEmpty(iconPath)) cfg.NormalIconPath = iconPath;
            
            // Reward patterns (optional - defaults work if not set)
            var supporterPattern = _getGlobal("cfg.supporterRewardPattern");
            if (!string.IsNullOrEmpty(supporterPattern)) cfg.SupporterRewardPattern = supporterPattern;
            
            var normalPattern = _getGlobal("cfg.normalRewardPattern");
            if (!string.IsNullOrEmpty(normalPattern)) cfg.NormalRewardPattern = normalPattern;
            
            // Hotkey patterns (optional - defaults work if not set)
            var dequeueHotkey = _getGlobal("cfg.dequeueHotkey");
            if (!string.IsNullOrEmpty(dequeueHotkey)) cfg.DequeueHotkey = dequeueHotkey;
            
            var refundHotkey = _getGlobal("cfg.refundTopHotkey");
            if (!string.IsNullOrEmpty(refundHotkey)) cfg.RefundTopHotkey = refundHotkey;

            return cfg;
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
