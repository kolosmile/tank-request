using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline {
  public bool Execute() {
    var json = CPH.GetGlobalVar<string>("tq.state", true);
    var st = string.IsNullOrEmpty(json) ? new LedgerState() : JsonConvert.DeserializeObject<LedgerState>(json) ?? new LedgerState();

    int count = st.normalQueue.Count;
    if (count == 0) {
      CPH.SendMessage("Nincs normál kérés a sorban.");
      return true;
    }

    int refunded = 0;
    foreach (var item in st.normalQueue) {
      if (!string.IsNullOrEmpty(item.redemptionId) && !string.IsNullOrEmpty(item.rewardId)) {
        try {
          CPH.TwitchRedemptionCancel(item.rewardId, item.redemptionId);
          refunded++;
        } catch (Exception ex) {
          CPH.LogWarn($"[RefundAllNormal] Cancel failed: {ex.Message}");
        }
      }
    }

    // Queue ürítése
    st.normalQueue.Clear();
    CPH.SetGlobalVar("tq.state", JsonConvert.SerializeObject(st), true);

    CPH.SendMessage($"{count} normál tank kérés visszavonva (pontok visszaosztva).");
    CPH.LogInfo($"[RefundAllNormal] Cleared {count} items, refunded {refunded} redemptions");

    // Overlay frissítés
    CPH.RunAction("Render Queue");
    return true;
  }

  class LedgerState { public List<QueueItem> supporterQueue = new(); public List<QueueItem> normalQueue = new(); }
  class QueueItem { public string user=""; public string tank=""; public int mult=1; public DateTime tsUtc=DateTime.UtcNow; public string raw=""; public string tipAmount=""; public string redemptionId=""; public string rewardId=""; }
}