using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline {
  public bool Execute() {
    var json = CPH.GetGlobalVar<string>("tq.state", true);
    var st = string.IsNullOrEmpty(json) ? new LedgerState() : JsonConvert.DeserializeObject<LedgerState>(json) ?? new LedgerState();

    QueueItem item = null; bool fromSupporter = false;
    if (st.supporterQueue.Count > 0) { item = st.supporterQueue[0]; st.supporterQueue.RemoveAt(0); fromSupporter = true; }
    else if (st.normalQueue.Count > 0) { item = st.normalQueue[0]; st.normalQueue.RemoveAt(0); }

    CPH.SetGlobalVar("tq.state", JsonConvert.SerializeObject(st), true);

    if (item == null) {
      CPH.SendMessage("A sor üres.");
    } else {
      // Fulfill: pontok levonása (csak Normal queue-nál, supporter már fulfill-olva van)
      if (!fromSupporter && !string.IsNullOrEmpty(item.redemptionId) && !string.IsNullOrEmpty(item.rewardId)) {
        try {
          CPH.TwitchRedemptionFulfill(item.rewardId, item.redemptionId);
          CPH.LogInfo($"[DequeueTop] Redemption fulfilled: {item.redemptionId}");
        } catch (Exception ex) {
          CPH.LogWarn($"[DequeueTop] Fulfill failed: {ex.Message}");
        }
      }

      var text = string.IsNullOrWhiteSpace(item.raw) ? (item.mult>1 ? $"{item.tank} x{item.mult}" : item.tank) : item.raw.Trim();
      CPH.SendMessage($"Teljesítve: {(fromSupporter?"[S]":"[N]")} {text} — {item.user}");
    }

    // Overlay frissítés
    CPH.RunAction("Render Queue");
    return true;
  }

  class LedgerState { public List<QueueItem> supporterQueue = new(); public List<QueueItem> normalQueue = new(); }
  class QueueItem { public string user=""; public string tank=""; public int mult=1; public DateTime tsUtc=DateTime.UtcNow; public string raw=""; public string tipAmount=""; public string redemptionId=""; public string rewardId=""; }
}