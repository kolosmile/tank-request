using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline {
  public bool Execute() {
    var json = CPH.GetGlobalVar<string>("tq.state", true);
    var st = string.IsNullOrEmpty(json) ? new LedgerState() : JsonConvert.DeserializeObject<LedgerState>(json) ?? new LedgerState();

    // Csak Normal queue-ból refund-olunk
    // Ha a supporter queue tetején van elem, nem csinálunk semmit
    if (st.supporterQueue.Count > 0) {
      return true;
    }

    if (st.normalQueue.Count == 0) {
      CPH.SendMessage("A normál sor üres.");
      return true;
    }

    // Normal queue refund
    QueueItem item = st.normalQueue[0];
    st.normalQueue.RemoveAt(0);

    CPH.SetGlobalVar("tq.state", JsonConvert.SerializeObject(st), true);

    // Refund: visszaadjuk a pontokat
    if (!string.IsNullOrEmpty(item.redemptionId) && !string.IsNullOrEmpty(item.rewardId)) {
      try {
        CPH.TwitchRedemptionCancel(item.rewardId, item.redemptionId);
        CPH.LogInfo($"[RefundTop] Redemption cancelled: {item.redemptionId}");
      } catch (Exception ex) {
        CPH.LogWarn($"[RefundTop] Cancel failed: {ex.Message}");
      }
    }

    var text = string.IsNullOrWhiteSpace(item.raw) ? item.tank : item.raw.Trim();
    CPH.SendMessage($"Visszavonva: [N] {text} — {item.user} (pont visszaadva)");

    // Overlay frissítés
    CPH.RunAction("Render Queue");
    return true;
  }

  class LedgerState { public List<QueueItem> supporterQueue = new(); public List<QueueItem> normalQueue = new(); }
  class QueueItem { public string user=""; public string tank=""; public int mult=1; public DateTime tsUtc=DateTime.UtcNow; public string raw=""; public string tipAmount=""; public string redemptionId=""; public string rewardId=""; }
}