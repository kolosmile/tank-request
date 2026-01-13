using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline {
  public bool Execute() {
    // user kontextus: először arg-okból, ha nincs akkor tmp.* fallback
    string userId   = CPH.GetGlobalVar<string>("tmp.userId", false)   ?? "";
    string userName = CPH.GetGlobalVar<string>("tmp.userName", false) ?? "";

    if (string.IsNullOrEmpty(userId)) {
      // ha "Test"-tel futtatod vagy nincs user kontextus
      CPH.SendMessage("Ezt a parancsot a chatből használd: !tank");
      return true;
    }

    var json = CPH.GetGlobalVar<string>("tq.state", true);
    var st = string.IsNullOrEmpty(json)
      ? new LedgerState()
      : JsonConvert.DeserializeObject<LedgerState>(json) ?? new LedgerState();

    st.users.TryGetValue(userId, out var u);

    int balance = 0;
    DateTime? nextExp = null;

    if (u != null) {
      var now = DateTime.UtcNow;
      var kept = new List<Bucket>();
      for (int i = 0; i < u.buckets.Count; i++) {
        var b = u.buckets[i];
        if (b.expiresAtUtc <= now) continue;
        kept.Add(b);
        balance += b.amount;
        if (nextExp == null || b.expiresAtUtc < nextExp.Value) nextExp = b.expiresAtUtc;
      }
      u.buckets = kept;
      CPH.SetGlobalVar("tq.state", JsonConvert.SerializeObject(st), true);
    }

    if (balance <= 0) {
      CPH.SendMessage($"{(string.IsNullOrEmpty(userName) ? "Nincs" : userName + ", nincs")} aktív támogatói tankkérésed.");
    } else {
      var expText = nextExp != null ? $" (köv. lejár: {nextExp.Value.ToLocalTime():yyyy.MM.dd HH:mm})" : "";
      CPH.SendMessage($"{(string.IsNullOrEmpty(userName) ? "Egyenleged" : userName + ", elérhető támogatói tankkéréseid")}: {balance}{expText}");
    }
    return true;
  }

  class LedgerState {
    public Dictionary<string,UserState> users = new();
    public List<QueueItem> supporterQueue = new();
    public List<QueueItem> normalQueue = new();
  }
  class UserState { public List<Bucket> buckets = new(); }
  class Bucket { public int amount; public DateTime expiresAtUtc; public string source=""; }
  class QueueItem { public string user=""; public string tank=""; public int mult=1; public DateTime tsUtc=DateTime.UtcNow; public string raw=""; public string tipAmount=""; }
}
