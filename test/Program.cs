// Teszt forgatókönyvek a TankRequest automatizációhoz
// Futtatás: dotnet run

using System;
using TankRequest.Core;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║       🎮 TankRequest Streamer.bot Teszt Környezet 🎮          ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var CPH = new MockCPH();
int passed = 0;
int failed = 0;

// ========================================
// TEST 1: Parse logika
// ========================================
TestSection("Parse Logika");

Test("Egyszerű tanknév", () => {
    var (tank, mult) = TQ.Parse("IS-7");
    Assert(tank == "IS-7", $"tank={tank}");
    Assert(mult == 1, $"mult={mult}");
});

Test("Tanknév szorzóval (x)", () => {
    var (tank, mult) = TQ.Parse("Obj 140 x3");
    Assert(tank == "Obj 140", $"tank={tank}");
    Assert(mult == 3, $"mult={mult}");
});

Test("Tanknév szorzóval (*)", () => {
    var (tank, mult) = TQ.Parse("T-62A *5");
    Assert(tank == "T-62A", $"mult={mult}");
    Assert(mult == 5, $"mult={mult}");
});

Test("Érvénytelen szorzó figyelmen kívül hagyása", () => {
    var (tank, mult) = TQ.Parse("E 100 x0");
    Assert(tank == "E 100 x0", $"tank={tank}"); // x0 nem érvényes, marad az egész
    Assert(mult == 1, $"mult={mult}");
});

Test("Kényszerített mult=1", () => {
    var (tank, mult) = TQ.Parse("Obj 140 x3", forceMult1: true);
    Assert(tank == "Obj 140 x3", $"tank={tank}");
    Assert(mult == 1, $"mult={mult}");
});

Test("Üres input", () => {
    var (tank, mult) = TQ.Parse("");
    Assert(tank == "", $"tank='{tank}'");
    Assert(mult == 1, $"mult={mult}");
});

// ========================================
// TEST 2: Token jóváírás
// ========================================
TestSection("Token Jóváírás (CreditTokens)");

Test("Tier 1 subscription = 1 token", () => {
    int tokens = TQ.CalculateTokens("Twitch", "subscription", tier: 1, giftCount: 0, bits: 0, tipAmount: 0);
    Assert(tokens == 1, $"tokens={tokens}");
});

Test("Tier 2 subscription = 2 token", () => {
    int tokens = TQ.CalculateTokens("Twitch", "subscription", tier: 2, giftCount: 0, bits: 0, tipAmount: 0);
    Assert(tokens == 2, $"tokens={tokens}");
});

Test("Tier 3 subscription = 6 token", () => {
    int tokens = TQ.CalculateTokens("Twitch", "subscription", tier: 3, giftCount: 0, bits: 0, tipAmount: 0);
    Assert(tokens == 6, $"tokens={tokens}");
});

Test("Gift bomb: 5x Tier 1 = 5 token", () => {
    int tokens = TQ.CalculateTokens("Twitch", "gift-bomb", tier: 1, giftCount: 5, bits: 0, tipAmount: 0);
    Assert(tokens == 5, $"tokens={tokens}");
});

Test("Gift bomb: 3x Tier 2 = 6 token", () => {
    int tokens = TQ.CalculateTokens("Twitch", "gift-bomb", tier: 2, giftCount: 3, bits: 0, tipAmount: 0);
    Assert(tokens == 6, $"tokens={tokens}");
});

Test("Cheer: 500 bits = 2 token (200 bits/token)", () => {
    int tokens = TQ.CalculateTokens("Twitch", "cheer", tier: 0, giftCount: 0, bits: 500, tipAmount: 0);
    Assert(tokens == 2, $"tokens={tokens}");
});

Test("Cheer: 150 bits = 0 token (nem elég)", () => {
    int tokens = TQ.CalculateTokens("Twitch", "cheer", tier: 0, giftCount: 0, bits: 150, tipAmount: 0);
    Assert(tokens == 0, $"tokens={tokens}");
});

Test("StreamElements tip: $9 = 3 token ($3/token)", () => {
    int tokens = TQ.CalculateTokens("StreamElements", "tip", tier: 0, giftCount: 0, bits: 0, tipAmount: 9.0);
    Assert(tokens == 3, $"tokens={tokens}");
});

// ========================================
// TEST 3: Balance és Consume
// ========================================
TestSection("Balance és Consume Logika");

Test("Aktív egyenleg számítás", () => {
    var user = new UserState {
        buckets = new() {
            new Bucket { amount = 3, expiresAtUtc = DateTime.UtcNow.AddHours(10) },
            new Bucket { amount = 2, expiresAtUtc = DateTime.UtcNow.AddHours(5) }
        }
    };
    int balance = TQ.ActiveBalance(user);
    Assert(balance == 5, $"balance={balance}");
});

Test("Lejárt token kiszűrése", () => {
    var user = new UserState {
        buckets = new() {
            new Bucket { amount = 3, expiresAtUtc = DateTime.UtcNow.AddHours(-1) }, // LEJÁRT
            new Bucket { amount = 2, expiresAtUtc = DateTime.UtcNow.AddHours(5) }
        }
    };
    int balance = TQ.ActiveBalance(user);
    Assert(balance == 2, $"balance={balance} (lejártak kiszűrve)");
    Assert(user.buckets.Count == 1, $"buckets.Count={user.buckets.Count}");
});

Test("Token fogyasztás sikeres", () => {
    var user = new UserState {
        buckets = new() {
            new Bucket { amount = 5, expiresAtUtc = DateTime.UtcNow.AddHours(10) }
        }
    };
    bool ok = TQ.Consume(user, 3);
    Assert(ok == true, $"ok={ok}");
    Assert(user.buckets[0].amount == 2, $"maradék={user.buckets[0].amount}");
});

Test("Token fogyasztás sikertelen (nincs elég)", () => {
    var user = new UserState {
        buckets = new() {
            new Bucket { amount = 2, expiresAtUtc = DateTime.UtcNow.AddHours(10) }
        }
    };
    bool ok = TQ.Consume(user, 5);
    Assert(ok == false, $"ok={ok}");
    Assert(user.buckets[0].amount == 2, $"amount marad={user.buckets[0].amount}");
});

Test("FIFO fogyasztás több bucket-ből", () => {
    var user = new UserState {
        buckets = new() {
            new Bucket { amount = 2, expiresAtUtc = DateTime.UtcNow.AddHours(5) },
            new Bucket { amount = 3, expiresAtUtc = DateTime.UtcNow.AddHours(10) }
        }
    };
    bool ok = TQ.Consume(user, 4);
    Assert(ok == true, $"ok={ok}");
    // Első bucket kiürült, második-ból 1 maradt
    Assert(user.buckets.Count == 1, $"buckets.Count={user.buckets.Count}");
    Assert(user.buckets[0].amount == 1, $"maradék={user.buckets[0].amount}");
});

// ========================================
// TEST 4: Teljes forgatókönyv (e2e)
// ========================================
TestSection("Teljes Forgatókönyv (End-to-End)");

Test("E2E: Támogatás -> Beváltás -> Egyenleg", () => {
    CPH.Reset();
    
    // 1. Üres state
    var st = TQ.Load(CPH);
    Assert(st.users.Count == 0, "Kezdetben nincs user");
    
    // 2. User kap 3 tokent (Tier 2 sub)
    string userId = "test-user-123";
    if (!st.users.TryGetValue(userId, out var user)) {
        user = new UserState();
        st.users[userId] = user;
    }
    int tokens = TQ.CalculateTokens("Twitch", "subscription", tier: 2, 0, 0, 0);
    user.buckets.Add(new Bucket { amount = tokens, expiresAtUtc = DateTime.UtcNow.AddHours(24), source = "sub" });
    TQ.Save(CPH, st);
    
    int balance = TQ.ActiveBalance(user);
    Assert(balance == 2, $"Sub után balance={balance}");
    
    // 3. User bevált 1 tokent
    bool consumed = TQ.Consume(user, 1);
    Assert(consumed == true, "Beváltás sikeres");
    
    int balanceAfter = TQ.ActiveBalance(user);
    Assert(balanceAfter == 1, $"Beváltás után balance={balanceAfter}");
    
    // 4. Queue-ba kerül a kérés
    var (tank, mult) = TQ.Parse("T-100 LT x1");
    st.supporterQueue.Add(new QueueItem { user = "TestUser", tank = tank, mult = mult, tsUtc = DateTime.UtcNow });
    Assert(st.supporterQueue.Count == 1, "Queue-ban van 1 elem");
    
    TQ.Save(CPH, st);
    Console.WriteLine($"   Final state: {CPH.GetStateJson().Substring(0, 100)}...");
});

// ========================================
// Összefoglaló
// ========================================
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  Összesen: {passed + failed} teszt  |  ✅ Sikeres: {passed}  |  ❌ Sikertelen: {failed}".PadRight(63) + "║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

if (failed > 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n⚠️  Vannak sikertelen tesztek! Ellenőrizd a logikát.");
    Console.ResetColor();
    Environment.Exit(1);
}
else
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n✅ Minden teszt sikeres! A logika működik.");
    Console.ResetColor();
}

// === HELPER METHODS ===

void TestSection(string name)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"━━━ {name} ━━━");
    Console.ResetColor();
}

void Test(string name, Action testFn)
{
    try
    {
        testFn();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✅ {name}");
        Console.ResetColor();
        passed++;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ❌ {name}: {ex.Message}");
        Console.ResetColor();
        failed++;
    }
}

void Assert(bool condition, string message = "")
{
    if (!condition)
        throw new Exception(message);
}
