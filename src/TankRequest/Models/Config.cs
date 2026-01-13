namespace TankRequest.Models
{
    /// <summary>
    /// Configuration loaded from Streamer.bot global variables (cfg.*)
    /// </summary>
    public class Config
    {
        // Token settings
        public int TtlHours { get; set; } = 24;
        public int BitsPerToken { get; set; } = 200;
        public int TipPerToken { get; set; } = 3;
        public int Tier1Tokens { get; set; } = 1;
        public int Tier2Tokens { get; set; } = 2;
        public int Tier3Tokens { get; set; } = 6;
        
        // Overlay settings
        public string QueueHtmlPath { get; set; } = @"C:\stream\tankqueue.html";
        public string NormalIconPath { get; set; } = "scheffton.png";
        public int QueueLines { get; set; } = 5;
        public int MaxTankNameLength { get; set; } = 15;
        
        // Reward name patterns (for auto-detection)
        public string SupporterRewardPattern { get; set; } = "supporter";
        public string NormalRewardPattern { get; set; } = "tank";
        
        // Hotkey detection - key value as Streamer.bot sends it
        public string DequeueHotkey { get; set; } = "shift+alt+ctrl+vcp";
        public string RefundTopHotkey { get; set; } = "shift+ctrl+vcr";
    }
}
