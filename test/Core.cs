// Közös modellek és logika - a scriptek által használt osztályok
// Ez a fájl tartalmazza a tényleges üzleti logikát, amit a scriptek és a tesztek is használnak

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TankRequest.Core
{
    // === DATA MODELS ===

    public class LedgerState
    {
        public Dictionary<string, UserState> users { get; set; } = new();
        public List<QueueItem> supporterQueue { get; set; } = new();
        public List<QueueItem> normalQueue { get; set; } = new();
    }

    public class UserState
    {
        public List<Bucket> buckets { get; set; } = new();
    }

    public class Bucket
    {
        public int amount { get; set; }
        public DateTime expiresAtUtc { get; set; }
        public string source { get; set; } = "";
    }

    public class QueueItem
    {
        public string user { get; set; } = "";
        public string tank { get; set; } = "";
        public int mult { get; set; } = 1;
        public DateTime tsUtc { get; set; } = DateTime.UtcNow;
        public string raw { get; set; } = "";
    }

    // === BUSINESS LOGIC ===

    public static class TQ
    {
        /// <summary>
        /// Parse a tankkérés szöveget: "Obj 140 x3" -> ("Obj 140", 3)
        /// </summary>
        public static (string tank, int mult) Parse(string raw, bool forceMult1 = false)
        {
            if (string.IsNullOrEmpty(raw)) return ("", 1);

            string s = raw.Trim();
            int mult = 1;
            string tank = s;

            if (!forceMult1)
            {
                int xPos = s.LastIndexOf('x');
                int star = s.LastIndexOf('*');
                int sep = xPos > star ? xPos : star;

                if (sep > 0)
                {
                    string left = s.Substring(0, sep).Trim();
                    string right = s.Substring(sep + 1).Trim();
                    if (int.TryParse(right, out int n) && n > 0)
                    {
                        mult = n;
                        tank = left;
                    }
                }
            }

            return (tank, mult);
        }

        /// <summary>
        /// Betölti a ledger state-et a CPH-ból
        /// </summary>
        public static LedgerState Load(MockCPH CPH)
        {
            string json = CPH.GetGlobalVar<string>("tq.state", true);
            return string.IsNullOrEmpty(json)
                ? new LedgerState()
                : JsonConvert.DeserializeObject<LedgerState>(json) ?? new LedgerState();
        }

        /// <summary>
        /// Elmenti a ledger state-et
        /// </summary>
        public static void Save(MockCPH CPH, LedgerState st)
        {
            CPH.SetGlobalVar("tq.state", JsonConvert.SerializeObject(st), true);
        }

        /// <summary>
        /// Kiszámítja az aktív (nem lejárt) egyenleget és kitakarítja a lejárt bucket-eket
        /// </summary>
        public static int ActiveBalance(UserState u)
        {
            var now = DateTime.UtcNow;
            var kept = new List<Bucket>();
            int sum = 0;

            foreach (var b in u.buckets)
            {
                if (b.expiresAtUtc > now)
                {
                    kept.Add(b);
                    sum += b.amount;
                }
            }

            u.buckets = kept;
            return sum;
        }

        /// <summary>
        /// Levon tokeneket a bucket-ekből (FIFO sorrendben)
        /// </summary>
        public static bool Consume(UserState u, int need)
        {
            var now = DateTime.UtcNow;

            // Először lejártak kiszűrése
            var kept = new List<Bucket>();
            foreach (var b in u.buckets)
            {
                if (b.expiresAtUtc > now)
                    kept.Add(b);
            }
            u.buckets = kept;

            // Elég van-e?
            int total = 0;
            foreach (var b in u.buckets) total += b.amount;
            if (total < need) return false;

            // Levonás FIFO
            int left = need;
            foreach (var b in u.buckets)
            {
                if (left <= 0) break;
                int take = Math.Min(b.amount, left);
                b.amount -= take;
                left -= take;
            }

            // Üres bucket-ek eltávolítása
            var kept2 = new List<Bucket>();
            foreach (var b in u.buckets)
            {
                if (b.amount > 0)
                    kept2.Add(b);
            }
            u.buckets = kept2;

            return left == 0;
        }

        /// <summary>
        /// Visszaadja a legközelebbi lejáratot
        /// </summary>
        public static DateTime? GetNextExpiry(UserState u)
        {
            DateTime? nextExp = null;
            foreach (var b in u.buckets)
            {
                if (nextExp == null || b.expiresAtUtc < nextExp.Value)
                    nextExp = b.expiresAtUtc;
            }
            return nextExp;
        }

        /// <summary>
        /// Token jóváírás támogatási esemény alapján
        /// </summary>
        public static int CalculateTokens(string eventSource, string eventType, int tier, int giftCount, int bits, double tipAmount,
            int t1Tok = 1, int t2Tok = 2, int t3Tok = 6, int bitsPerTok = 200, int tipPerTok = 3)
        {
            int tokens = 0;

            if (eventSource == "Twitch")
            {
                if (eventType == "subscription" || eventType == "resubscription" ||
                    eventType == "prime-paid-upgrade" || eventType == "gift-paid-upgrade" ||
                    eventType == "gift-subscription")
                {
                    tokens = tier == 3 ? t3Tok : (tier == 2 ? t2Tok : t1Tok);
                }
                else if (eventType == "gift-bomb")
                {
                    int per = tier == 3 ? t3Tok : (tier == 2 ? t2Tok : t1Tok);
                    tokens = per * giftCount;
                }
                else if (eventType == "cheer")
                {
                    tokens = bitsPerTok > 0 ? bits / bitsPerTok : 0;
                }
            }
            else if (eventSource == "StreamElements" && eventType == "tip")
            {
                tokens = tipPerTok > 0 ? (int)Math.Floor(tipAmount / tipPerTok) : 0;
            }

            return tokens;
        }
    }
}
