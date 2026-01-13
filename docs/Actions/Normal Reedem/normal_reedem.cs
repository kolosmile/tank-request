using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline {
  string G(string key) { try { return CPH.GetGlobalVar<string>(key, false) ?? ""; } catch { return ""; } }
  // Lokális modellek, hogy ne legyen névütközés
  class LedgerState {
    public Dictionary<string,UserState> users = new();
    public List<QueueItem> supporterQueue = new();
    public List<QueueItem> normalQueue = new();
  }
  class UserState { public List<Bucket> buckets = new(); }
  class Bucket { public int amount; public DateTime expiresAtUtc; public string source=""; }
  class QueueItem { public string user=""; public string tank=""; public int mult=1; public DateTime tsUtc=DateTime.UtcNow; public string raw=""; public string tipAmount=""; public string redemptionId=""; public string rewardId=""; }

  static class TQ {
    public static (string tank,int mult) Parse(string raw, bool forceMult1=false) {
      if (string.IsNullOrEmpty(raw)) return ("",1);
      string s = raw.Trim();
      int mult = 1;
      string tank = s;
      if (!forceMult1) {
        int xPos = s.LastIndexOf('x');
        int star = s.LastIndexOf('*');
        int sep = xPos>star ? xPos : star;
        if (sep>0) {
          string left = s.Substring(0, sep).Trim();
          string right = s.Substring(sep+1).Trim();
          int n;
          if (int.TryParse(right, out n) && n>0) { mult=n; tank=left; }
        }
      }
      return (tank, mult);
    }
    public static LedgerState Load(CPHInline ctx) {
      string json = ctx.CPH.GetGlobalVar<string>("tq.state", true);
      return string.IsNullOrEmpty(json) ? new LedgerState() : JsonConvert.DeserializeObject<LedgerState>(json) ?? new LedgerState();
    }
    public static void Save(CPHInline ctx, LedgerState st) { ctx.CPH.SetGlobalVar("tq.state", JsonConvert.SerializeObject(st), true); }
    public static int ActiveBalance(UserState u) {
      var now = DateTime.UtcNow;
      var kept = new List<Bucket>();
      int sum=0;
      for (int i=0;i<u.buckets.Count;i++) {
        var b=u.buckets[i];
        if (b.expiresAtUtc>now) { kept.Add(b); sum+=b.amount; }
      }
      u.buckets = kept;
      return sum;
    }
    public static bool Consume(UserState u, int need) {
      var now = DateTime.UtcNow;
      var kept = new List<Bucket>();
      for (int i=0;i<u.buckets.Count;i++) if (u.buckets[i].expiresAtUtc>now) kept.Add(u.buckets[i]);
      u.buckets = kept;
      int left=need;
      for (int i=0;i<u.buckets.Count && left>0;i++) {
        var b=u.buckets[i];
        int take = b.amount < left ? b.amount : left;
        b.amount -= take; left -= take;
      }
      var kept2 = new List<Bucket>();
      for (int i=0;i<u.buckets.Count;i++) if (u.buckets[i].amount>0) kept2.Add(u.buckets[i]);
      u.buckets = kept2;
      return left==0;
    }
  }

  public bool Execute() {
  string userName = G("tmp.userName");
  string raw = G("tmp.rawInput");
  var (tank, _) = TQ.Parse(raw, forceMult1:true); // mindig 1

    if (string.IsNullOrWhiteSpace(tank)) {
      CPH.SetArgument("displayMsg","Adj meg egy tanknevet! Pl.: 'IS-7'");
      CPH.SetArgument("allow","false");
      return true;
    }

    if (tank.Length > 15) {
      CPH.SetArgument("displayMsg","Túl hosszú a tanknév (max 15 karakter)!");
      CPH.SetArgument("allow","false");
      return true;
    }

  var st = TQ.Load(this);
  string rawInput = string.IsNullOrEmpty(raw) ? (G("tmp.rawInput") ?? "") : raw;
  string redemptionId = G("redemptionId");
  string rewardId = G("rewardId");
    st.normalQueue.Add(new QueueItem{ user=userName, tank=tank, mult=1, tsUtc=DateTime.UtcNow, raw=rawInput, redemptionId=redemptionId, rewardId=rewardId });
  TQ.Save(this, st);
    try { CPH.RunAction("TankRequests - Render Queue"); } catch {}
    CPH.SetArgument("displayMsg",$"Felvéve: [N] {tank} – {userName}");
    CPH.LogInfo($"[NormalRedeem] OK user={userName} tank='{tank}'");
    return true;
  }
}
