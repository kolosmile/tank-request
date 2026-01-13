using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline {
  string HtmlEncode(string s) {
    if (string.IsNullOrEmpty(s)) return "";
    return s.Replace("&","&amp;").Replace("<","&lt;")
            .Replace(">","&gt;").Replace("\"","&quot;")
            .Replace("'","&#39;");
  }

  public bool Execute() {
    // --- Konfigok ---
    string path = CPH.GetGlobalVar<string>("cfg.queueHtmlPath", true);
    if (string.IsNullOrWhiteSpace(path)) path = @"C:\stream\tankqueue.html";
    
    string iconPath = CPH.GetGlobalVar<string>("cfg.normalIconPath", true);
    if (string.IsNullOrWhiteSpace(iconPath)) iconPath = "scheffton.png";
    
    int topN = 5;
    try {
      var nStr = CPH.GetGlobalVar<string>("cfg.queueLines", true);
      if (!string.IsNullOrWhiteSpace(nStr)) {
        int nParsed; if (int.TryParse(nStr, out nParsed) && nParsed>0) topN = nParsed;
      } else {
        var nOpt = CPH.GetGlobalVar<int?>("cfg.queueLines", true);
        if (nOpt.HasValue && nOpt.Value>0) topN = nOpt.Value;
      }
    } catch {}

    // --- Állapot betöltés ---
    var json = CPH.GetGlobalVar<string>("tq.state", true);
    var st = string.IsNullOrEmpty(json) ? new LedgerState() :
             JsonConvert.DeserializeObject<LedgerState>(json) ?? new LedgerState();

    // --- Top N összeállítás: előbb supporter, aztán normal ---
    var items = new List<(QueueItem it, bool isSupporter)>();
    for (int i=0; i<st.supporterQueue.Count && items.Count<topN; i++)
      items.Add((st.supporterQueue[i], true));
    for (int i=0; i<st.normalQueue.Count && items.Count<topN; i++)
      items.Add((st.normalQueue[i], false));

    int totalRemaining = st.supporterQueue.Count + st.normalQueue.Count - items.Count;

    // --- HTML render ---
    var sb = new StringBuilder();
    sb.AppendLine("<!doctype html><html><head><meta charset='utf-8'>");
    sb.AppendLine("<meta http-equiv='refresh' content='2'>");
    sb.AppendLine("<style>");
    sb.AppendLine("* { box-sizing: border-box; margin: 0; padding: 0; }");
    sb.AppendLine("html, body { background: transparent; font-family: 'Exo 2', 'Segoe UI', Arial, sans-serif; text-transform: uppercase; }");
    sb.AppendLine(".container { display: flex; flex-direction: column; gap: 6px; padding: 10px; width: fit-content; }");
    sb.AppendLine(".queue-item { display: flex; align-items: center; gap: 0; filter: drop-shadow(0 2px 8px rgba(0,0,0,0.4)); }");
    
    // Fekete háttér #171717, nem áttetsző, egyforma magasság
    sb.AppendLine(".icon-box { width: 50px; height: 50px; border-radius: 12px 0 0 12px; display: flex; align-items: center; justify-content: center; background: #171717; border-right: 3px solid #46c89e; }");
    sb.AppendLine(".icon-box img { width: 24px; height: 24px; object-fit: contain; }");
    
    // Csillag és tip összeg: #46c89e
    sb.AppendLine(".star { font-size: 28px; color: #46c89e; text-shadow: 0 0 8px rgba(70,200,158,0.5); }");
    sb.AppendLine(".amount { font-size: 16px; font-weight: 700; color: #46c89e; }");
    
    // Text box - egyforma magasság (50px), 5% hosszabb (min-width: 210px)
    sb.AppendLine(".text-box { background: #171717; border-radius: 0 12px 12px 0; padding: 0 10px; min-width: 210px; height: 50px; display: flex; align-items: center; }");
    sb.AppendLine(".tank-name { color: white; font-size: 20px; font-weight: 500; letter-spacing: 0.5px; }");
    
    // Footer
    sb.AppendLine(".queue-footer { color: rgba(255,255,255,0.5); font-size: 16px; padding-left: 60px; margin-top: 4px; }");
    sb.AppendLine(".empty-msg { color: rgba(255,255,255,0.4); font-size: 18px; padding: 10px 20px; }");
    
    sb.AppendLine("</style></head><body>");
    sb.AppendLine("<div class='container'>");

    if (items.Count==0) {
      sb.AppendLine("<div class='empty-msg'>Nincs kérés a sorban</div>");
    } else {
      for (int i=0; i<items.Count; i++) {
        var it = items[i].it;
        bool isSupporter = items[i].isSupporter;
        
        var display = string.IsNullOrWhiteSpace(it.raw)
          ? it.tank + (it.mult>1 ? $" x{it.mult}" : "")
          : it.raw.Trim();

        sb.AppendLine("<div class='queue-item'>");
        
        if (isSupporter) {
          // Támogatói: tipAmount > 0 → összeg, különben csillag
          if (!string.IsNullOrEmpty(it.tipAmount) && it.tipAmount != "0") {
            sb.AppendLine($"  <div class='icon-box'><span class='amount'>{HtmlEncode(it.tipAmount)}</span></div>");
          } else {
            sb.AppendLine("  <div class='icon-box'><span class='star'>★</span></div>");
          }
        } else {
          // Normál kérés: scheffton.png
          sb.AppendLine($"  <div class='icon-box'><img src='{HtmlEncode(iconPath)}' alt='icon'></div>");
        }
        
        sb.AppendLine($"  <div class='text-box'><span class='tank-name'>{HtmlEncode(display)}</span></div>");
        sb.AppendLine("</div>");
      }
    }

    // Footer
    if (totalRemaining > 0) {
      sb.AppendLine($"<div class='queue-footer'>+{totalRemaining} a sorban</div>");
    }

    sb.AppendLine("</div></body></html>");

    // --- Írás ---
    try {
      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
      File.WriteAllText(path, sb.ToString());
      CPH.LogInfo($"Queue HTML frissítve: {path}");
    } catch (Exception ex) {
      CPH.LogWarn($"Queue HTML írási hiba: {ex.Message}");
    }
    return true;
  }

  class LedgerState {
    public List<QueueItem> supporterQueue = new();
    public List<QueueItem> normalQueue = new();
  }
  class QueueItem {
    public string user=""; public string tank=""; public int mult=1;
    public DateTime tsUtc=DateTime.UtcNow; public string raw="";
    public string tipAmount=""; public string redemptionId="";
  }
}