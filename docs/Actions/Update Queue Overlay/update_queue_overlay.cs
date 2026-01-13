using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline {
  public bool Execute() {
    var path = CPH.GetGlobalVar<string>("cfg.queueFile", true);
    if (string.IsNullOrWhiteSpace(path)) path = @"C:\stream\tankqueue.txt";
    try {
      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    } catch {}

    var json = CPH.GetGlobalVar<string>("tq.state", true);
    var st = string.IsNullOrEmpty(json) ? new LedgerState() : JsonConvert.DeserializeObject<LedgerState>(json) ?? new LedgerState();

    var lines = new List<string>();
    int idx = 1;

    foreach (var it in st.supporterQueue) {
      var text = string.IsNullOrWhiteSpace(it.raw) ? $"{it.tank}{(it.mult>1 ? $" x{it.mult}" : "")}" : it.raw.Trim();
      lines.Add($"{idx,2}. [S] {text} — {it.user}");
      idx++;
    }
    foreach (var it in st.normalQueue) {
      var text = string.IsNullOrWhiteSpace(it.raw) ? it.tank : it.raw.Trim();
      lines.Add($"{idx,2}. [N] {text} — {it.user}");
      idx++;
    }
    if (lines.Count == 0) lines.Add("(üres sor)");

    File.WriteAllText(path, string.Join(Environment.NewLine, lines));
    return true;
  }

  // modellek
  class LedgerState { public List<QueueItem> supporterQueue = new(); public List<QueueItem> normalQueue = new(); }
  class QueueItem { public string user=""; public string tank=""; public int mult=1; public DateTime tsUtc=DateTime.UtcNow; public string raw=""; public string tipAmount=""; }
}
