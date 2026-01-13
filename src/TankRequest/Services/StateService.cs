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
    }
}
