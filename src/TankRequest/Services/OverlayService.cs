namespace TankRequest.Services
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using TankRequest.Models;

    /// <summary>
    /// Generates HTML overlay for OBS Browser Source.
    /// </summary>
    public class OverlayService
    {
        private readonly Config _config;

        public OverlayService(Config config)
        {
            _config = config;
        }

        /// <summary>
        /// Generate and save HTML overlay file.
        /// </summary>
        public void RenderQueue(LedgerState state)
        {
            var items = GetDisplayItems(state, _config.QueueLines);
            int totalCount = state.supporterQueue.Count + state.normalQueue.Count;
            int remaining = totalCount - items.Count;

            var html = GenerateHtml(items, remaining);
            File.WriteAllText(_config.QueueHtmlPath, html, Encoding.UTF8);
        }

        private List<(QueueItem item, bool isSupporter)> GetDisplayItems(LedgerState state, int max)
        {
            var items = new List<(QueueItem, bool)>();
            
            foreach (var item in state.supporterQueue)
            {
                if (items.Count >= max) break;
                items.Add((item, true));
            }
            
            foreach (var item in state.normalQueue)
            {
                if (items.Count >= max) break;
                items.Add((item, false));
            }

            return items;
        }

        private string GenerateHtml(List<(QueueItem item, bool isSupporter)> items, int remaining)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!doctype html><html><head><meta charset='utf-8'>");
            sb.AppendLine("<link href='https://fonts.googleapis.com/css2?family=Exo+2:wght@400;600;700&display=swap' rel='stylesheet'>");
            sb.AppendLine("<meta http-equiv='refresh' content='2'>");
            sb.AppendLine("<style>");
            sb.AppendLine("* { box-sizing: border-box; margin: 0; padding: 0; }");
            sb.AppendLine("html, body { background: transparent; font-family: 'Exo 2', 'Segoe UI', Arial, sans-serif; text-transform: uppercase; }");
            sb.AppendLine(".container { display: flex; flex-direction: column; gap: 6px; padding: 10px; width: fit-content; }");
            sb.AppendLine(".queue-item { display: flex; align-items: center; gap: 0; filter: drop-shadow(0 2px 8px rgba(0,0,0,0.4)); }");
            sb.AppendLine(".icon-box { width: 50px; height: 50px; border-radius: 12px 0 0 12px; display: flex; align-items: center; justify-content: center; background: #171717; border-right: 3px solid #46c89e; }");
            sb.AppendLine(".icon-box img { width: 24px; height: 24px; object-fit: contain; }");
            sb.AppendLine(".star { font-size: 28px; color: #46c89e; text-shadow: 0 0 8px rgba(70,200,158,0.5); line-height: 1; transform: translateY(-2px); }");
            sb.AppendLine(".amount { font-size: 16px; font-weight: 700; color: #46c89e; }");
            sb.AppendLine(".text-box { background: #171717; border-radius: 0 12px 12px 0; padding: 0 20px; min-width: 210px; height: 50px; display: flex; align-items: center; }");
            sb.AppendLine(".tank-name { color: white; font-size: 22px; font-weight: 400; letter-spacing: 1.5px; }");
            sb.AppendLine(".queue-footer { color: rgba(255,255,255,0.5); font-size: 16px; padding-left: 60px; margin-top: 4px; }");
            sb.AppendLine(".empty-msg { color: rgba(255,255,255,0.4); font-size: 18px; padding: 10px 20px; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<div class='container'>");

            if (items.Count == 0)
            {
                sb.AppendLine("<div class='empty-msg'>Nincs kérés a sorban</div>");
            }
            else
            {
                foreach (var (item, isSupporter) in items)
                {
                    sb.AppendLine("<div class='queue-item'>");
                    
                    // Icon box
                    sb.Append("<div class='icon-box'>");
                    if (isSupporter)
                    {
                        if (!string.IsNullOrEmpty(item.tipAmount))
                            sb.Append($"<span class='amount'>{item.tipAmount}</span>");
                        else if (item.specialType == "Arty")
                            sb.Append("<span class='star'>A</span>");
                        else if (item.specialType == "Blacklist")
                            sb.Append("<span class='star'>B</span>");
                        else if (item.specialType == "Troll")
                            sb.Append("<span class='star'>T</span>");
                        else
                            sb.Append("<span class='star'>★</span>");
                    }
                    else
                    {
                        sb.Append($"<img src='{_config.NormalIconPath}' />");
                    }
                    sb.AppendLine("</div>");

                    // Text box - no special type suffix, just tank name and multiplier
                    var displayText = item.tank;
                    if (item.mult > 1) displayText += $" x{item.mult}";

                    string nameStyle = isSupporter ? "style='color: #3bf4ba;'" : "";
                    sb.AppendLine($"<div class='text-box'><span class='tank-name' {nameStyle}>{displayText}</span></div>");
                    
                    sb.AppendLine("</div>");
                }

                if (remaining > 0)
                {
                    sb.AppendLine($"<div class='queue-footer'>+{remaining} a sorban</div>");
                }
            }

            sb.AppendLine("</div></body></html>");
            return sb.ToString();
        }
    }
}
