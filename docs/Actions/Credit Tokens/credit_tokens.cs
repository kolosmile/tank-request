using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline {
  int GetInt(string key, int d) { try { return CPH.GetGlobalVar<int?>($"cfg.{key}", true) ?? d; } catch { return d; } }
  string G(string key) { try { return CPH.GetGlobalVar<string>(key, false) ?? ""; } catch { return ""; } }
  int GInt(string key) { int v=0; int.TryParse(G(key), out v); return v; }
  double GDbl(string key) { double v=0; double.TryParse(G(key), out v); return v; }

  int GetTier() {
    int t = GInt("tmp.tier");
    if (t>=1 && t<=3) return t;
    string plan = G("tmp.plan");
    if (plan == "1000") return 1;
    if (plan == "2000") return 2;
    if (plan == "3000") return 3;
    return 1;
  }

  int GetGiftCount() {
    int n = GInt("tmp.giftCount");
    if (n <= 0) n = GInt("tmp.gifts");
    if (n <= 0) n = GInt("tmp.count");
    return Math.Max(n, 1);
  }

  public bool Execute() {
    // cfg-k
  int ttlHours   = GetInt("ttlHours", 24);
  int bitsPerTok = GetInt("bitsPerToken", 200);
  int tipPerTok  = GetInt("tipPerToken", 3);
  int t1Tok      = GetInt("tier1Tokens", 1);
  int t2Tok      = GetInt("tier2Tokens", 2);
  int t3Tok      = GetInt("tier3Tokens", 6);

  // Teszt futtatáshoz: tmp.eventSource, tmp.eventType, tmp.userId
  string ev   = G("tmp.eventSource"); // "Twitch" / "StreamElements"
  string type = G("tmp.eventType");
  string userId = G("tmp.userId");
    if (string.IsNullOrEmpty(userId)) return true; // anon/hibás

    int tokens = 0;

    if (ev=="Twitch" && (type=="subscription" || type=="resubscription" 
                       || type=="prime-paid-upgrade" || type=="gift-paid-upgrade")) {
  int tier = GetTier();
      tokens = tier==3 ? t3Tok : (tier==2 ? t2Tok : t1Tok);

    } else if (ev=="Twitch" && type=="gift-subscription") {
  int tier = GetTier();
      tokens = tier==3 ? t3Tok : (tier==2 ? t2Tok : t1Tok);

    } else if (ev=="Twitch" && type=="gift-bomb") {
  int tier = GetTier();
  int count = GetGiftCount();
      int per   = (tier==3 ? t3Tok : (tier==2 ? t2Tok : t1Tok));
      tokens = per * count;

    } else if (ev=="Twitch" && type=="cheer") {
      int bits = GInt("tmp.bits");
      tokens = (bitsPerTok > 0) ? (bits / bitsPerTok) : 0;

    } else if (ev=="StreamElements" && type=="tip") {
      double amount = GDbl("tmp.amount");
      tokens = (tipPerTok > 0) ? (int)Math.Floor(amount / tipPerTok) : 0;
      // Tip összeg mentése a queue-hoz (supporter_redeem fogja használni)
      string currency = G("tmp.currency");
      if (string.IsNullOrEmpty(currency)) currency = "€";
      CPH.SetGlobalVar("tmp.lastTipAmount", $"{amount:0.##}{currency}", false);
    }

    if (tokens <= 0) return true;

    // ledger betöltés
  var json = CPH.GetGlobalVar<string>("tq.state", true);
    var st = string.IsNullOrEmpty(json) ? new LedgerState()
       : JsonConvert.DeserializeObject<LedgerState>(json) ?? new LedgerState();

    if (!st.users.TryGetValue(userId, out var u)) { u = new UserState(); st.users[userId] = u; }

    // lejártak kidobása + új bucket
    var now = DateTime.UtcNow;
    var kept = new List<Bucket>();
    for (int i=0; i<u.buckets.Count; i++) if (u.buckets[i].expiresAtUtc > now) kept.Add(u.buckets[i]);
    u.buckets = kept;
    u.buckets.Add(new Bucket { amount = tokens, expiresAtUtc = now.AddHours(ttlHours), source = "credit" });

    // egyenleg és legközelebbi lejárat kézzel
    int balance = 0; DateTime? nextExp = null;
    for (int i=0; i<u.buckets.Count; i++) {
      var b = u.buckets[i];
      balance += b.amount;
      if (nextExp==null || b.expiresAtUtc < nextExp.Value) nextExp = b.expiresAtUtc;
    }

    // mentés + üzenet
    CPH.SetGlobalVar("tq.state", JsonConvert.SerializeObject(st), true);
    var expText = nextExp != null ? $" (köv. lejár: {nextExp.Value.ToLocalTime():yyyy.MM.dd HH:mm})" : "";
    // Chat és log a teszthez
    CPH.SendMessage($"+{tokens} támogatói token. Egyenleg: {balance}{expText}");
    CPH.LogInfo($"[CreditTokens] userId={userId} +{tokens} (balance={balance}) ev={ev} type={type}");
    return true;
  }

  // minimál modellek
  class LedgerState {
    public System.Collections.Generic.Dictionary<string,UserState> users = new();
    public System.Collections.Generic.List<QueueItem> supporterQueue = new();
    public System.Collections.Generic.List<QueueItem> normalQueue = new();
  }
  class UserState { public System.Collections.Generic.List<Bucket> buckets = new(); }
  class Bucket { public int amount; public DateTime expiresAtUtc; public string source=""; }
  class QueueItem { public string user=""; public string tank=""; public int mult=1; public DateTime tsUtc=DateTime.UtcNow; public string raw=""; public string tipAmount=""; }
}
