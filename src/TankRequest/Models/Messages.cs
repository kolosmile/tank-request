namespace TankRequest.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// All chat messages used by the bot.
    /// Messages can be customized via Streamer.bot global variables (msg.*).
    /// Each message uses {placeholder} syntax for dynamic values.
    /// </summary>
    public class Messages
    {
        // ============================================
        // TOKEN MESSAGES
        // ============================================

        /// <summary>
        /// Token jóváírás (sub/cheer/tip).
        /// Placeholders: {user}, {amount}, {balance}
        /// </summary>
        public string TokensCredited { get; set; } = "@{user}, +{amount} tokent kaptál. Egyenleg: {balance}tk. Kérj ki egy tankot!";

        /// <summary>
        /// Token hozzáadva (!addtokens).
        /// Placeholders: {user}, {amount}, {balance}
        /// </summary>
        public string TokensAdded { get; set; } = "@{user}, +{amount} tokent kaptál. Egyenleg: {balance}tk.";

        /// <summary>
        /// Token eltávolítva (!removetokens).
        /// Placeholders: {user}, {amount}, {balance}
        /// </summary>
        public string TokensRemoved { get; set; } = "@{user}, -{amount}tk levonva. Egyenleg: {balance}tk.";

        /// <summary>
        /// Nincs elég token.
        /// Placeholders: {balance}, {cost}
        /// </summary>
        public string NotEnoughTokens { get; set; } = "Nincs elég tokened. Egyenleg: {balance}tk, Szükséges: {cost}tk.";

        // ============================================
        // TANKINFO MESSAGES
        // ============================================

        /// <summary>
        /// Tankinfo egyenleggel.
        /// Placeholders: {user}, {target}, {balance}, {expiry}, {queueInfo}
        /// </summary>
        public string TankInfoBalance { get; set; } = "@{user}, {target}Egyenleg: {balance}tk (lejár: {expiry}).{queueInfo}";

        /// <summary>
        /// Tankinfo: nincs token, de van queue pozíció.
        /// Placeholders: {user}, {target}, {queueInfo}
        /// </summary>
        public string TankInfoNoTokensInQueue { get; set; } = "@{user}, {target}Nincs tokened, de van kérésed a sorban.{queueInfo}";

        /// <summary>
        /// Tankinfo: nincs semmi.
        /// Placeholders: {user}, {target}
        /// </summary>
        public string TankInfoEmpty { get; set; } = "@{user}, {target}Nincs tokened és nincs kérésed a sorban.";

        // ============================================
        // QUEUE MESSAGES
        // ============================================

        /// <summary>
        /// Supporter kérés hozzáadva.
        /// Placeholders: {tank}, {cost}, {user}, {balance}
        /// </summary>
        public string SupporterAdded { get; set; } = "@{user} Támogatói kérés sorhoz adva: {tank} x{cost}. Egyenleg: {balance}tk";

        /// <summary>
        /// Arty kérés hozzáadva.
        /// Placeholders: {tank}, {user}, {balance}
        /// </summary>
        public string ArtyAdded { get; set; } = "@{user} Arty kérés sorhoz adva: {tank} (-5tk). Egyenleg: {balance}tk";

        /// <summary>
        /// Blacklist kérés hozzáadva.
        /// Placeholders: {tank}, {user}, {balance}
        /// </summary>
        public string BlacklistAdded { get; set; } = "@{user} Feketelistás kérés sorhoz adva: {tank} (-3tk). Egyenleg: {balance}tk";

        /// <summary>
        /// Troll kérés hozzáadva.
        /// Placeholders: {tank}, {user}, {balance}
        /// </summary>
        public string TrollAdded { get; set; } = "@{user} Troll kérés sorhoz adva: {tank} (-10tk). Egyenleg: {balance}tk";

        /// <summary>
        /// Normál kérés hozzáadva.
        /// Placeholders: {tank}, {user}
        /// </summary>
        public string NormalAdded { get; set; } = "@{user} Normál kérés sorhoz adva: {tank}";

        /// <summary>
        /// Kérés teljesítve.
        /// Placeholders: {type}, {tank}, {user}
        /// </summary>
        public string Completed { get; set; } = "@{user} Teljesítve: {type} {tank}";

        /// <summary>
        /// Normál kérés visszavonva.
        /// Placeholders: {tank}, {user}
        /// </summary>
        public string RefundedNormal { get; set; } = "@{user} Visszavonva: [N] {tank} (pont visszaadva)";

        /// <summary>
        /// Összes normál visszavonva.
        /// Placeholders: {count}
        /// </summary>
        public string RefundedAllNormal { get; set; } = "{count} normál kérés visszavonva, pontok visszaadva.";

        /// <summary>
        /// Manuális normál hozzáadás.
        /// Placeholders: {tank}, {user}
        /// </summary>
        public string ManualNormalAdded { get; set; } = "[MANUAL] Felvéve: [N] {tank} – {user}";

        /// <summary>
        /// Sor üres.
        /// </summary>
        public string QueueEmpty { get; set; } = "A sor üres.";

        /// <summary>
        /// Nincs normál kérés.
        /// </summary>
        public string NoNormalRequests { get; set; } = "Nincs normál kérés a sorban.";

        // ============================================
        // ERROR MESSAGES
        // ============================================

        /// <summary>
        /// Hiba üzenet felhasználónak.
        /// Placeholders: {user}, {error}
        /// </summary>
        public string Error { get; set; } = "@{user}, {error}";

        /// <summary>
        /// Csak mod/broadcaster használhatja.
        /// Placeholders: {user}
        /// </summary>
        public string ModOnly { get; set; } = "@{user}, csak mod/broadcaster használhatja ezt a parancsot.";

        /// <summary>
        /// Felhasználó nem található.
        /// Placeholders: {user}, {target}
        /// </summary>
        public string UserNotFound { get; set; } = "@{user}, {target} nem található.";

        /// <summary>
        /// Target user nincs elég tokenje (!queuesupporter @user).
        /// Placeholders: {target}, {balance}, {cost}
        /// </summary>
        public string TargetNotEnoughTokens { get; set; } = "@{target} nem rendelkezik elég tokennel (van: {balance}, kell: {cost}).";

        /// <summary>
        /// Hiányzó tanknév.
        /// </summary>
        public string TankNameMissing { get; set; } = "Adj meg egy tanknevet! Pl.: 'IS-7'";

        /// <summary>
        /// Túl hosszú tanknév.
        /// Placeholders: {maxLength}
        /// </summary>
        public string TankNameTooLong { get; set; } = "Túl hosszú a tanknév (max {maxLength} karakter)!";

        // ============================================
        // USAGE MESSAGES
        // ============================================

        /// <summary>
        /// !addtokens használat.
        /// Placeholders: {user}
        /// </summary>
        public string UsageAddTokens { get; set; } = "@{user}, használat: !addtokens [mennyiség] vagy !addtokens [felhasználó] [mennyiség]";

        /// <summary>
        /// !removetokens használat.
        /// Placeholders: {user}
        /// </summary>
        public string UsageRemoveTokens { get; set; } = "@{user}, használat: !removetokens [mennyiség] vagy !removetokens [felhasználó] [mennyiség]";

        /// <summary>
        /// !addnorm használat.
        /// Placeholders: {user}
        /// </summary>
        public string UsageQueueNormal { get; set; } = "@{user}, használat: !queuenormal [tank név]";

        /// <summary>
        /// !addsupp használat.
        /// Placeholders: {user}
        /// </summary>
        public string UsageQueueSupporter { get; set; } = "@{user}, használat: !queuesupporter [@user] [tank név] [szorzó/kód]";

        // ============================================
        // HELP MESSAGES
        // ============================================

        /// <summary>
        /// Tank help első sor.
        /// Placeholders: {tier1}, {tier2}, {tier3}, {bitsPerToken}, {tipPerToken}
        /// </summary>
        public string HelpLine1 { get; set; } = " Így kérhetsz tankot: 1. Normál: Csatornapontból. 2. Támogatói: Tokenekkel (⭐prioritás). 🪙Token jár: Sub (T1={tier1}tk, T2={tier2}tk, T3={tier3}tk), Cheer ({bitsPerToken}b=1tk), Tip ({tipPerToken}€=1tk).";

        /// <summary>
        /// Tank help második sor.
        /// Placeholders: {ttlHours}, {costArty}, {costBlacklist}, {costTroll}
        /// </summary>
        public string HelpLine2 { get; set; } = "🕒A tokenek {ttlHours} óráig érvényesek! ⚠️Speciális: xA (Arty, {costArty}tk), xB (Blacklist, {costBlacklist}tk), xT (Troll, {costTroll}tk). 📈Többszörös Bombardino pontért használj szorzót (pl. Tiger x3). Egyenleg: !tankinfo";

        // ============================================
        // QUEUE POSITION MESSAGES
        // ============================================

        /// <summary>
        /// Pozíció: épp csatában.
        /// Placeholders: {pos}
        /// </summary>
        public string QueuePosActive { get; set; } = " Pozíció: {pos}. Épp csatában.";

        /// <summary>
        /// Pozíció: hamarosan.
        /// Placeholders: {pos}
        /// </summary>
        public string QueuePosSoon { get; set; } = " Pozíció: {pos}. Hamarosan sorra kerülsz.";

        /// <summary>
        /// Pozíció: várakozás.
        /// Placeholders: {pos}, {eta}
        /// </summary>
        public string QueuePosWait { get; set; } = " Pozíció: {pos}. (kb. {eta} múlva)";

        // ============================================
        // FORMAT HELPER
        // ============================================

        /// <summary>
        /// Format a message template with named placeholders.
        /// </summary>
        public static string Format(string template, params (string key, object value)[] values)
        {
            if (string.IsNullOrEmpty(template)) return template;
            
            foreach (var (key, value) in values)
            {
                template = template.Replace("{" + key + "}", value?.ToString() ?? "");
            }
            
            return template;
        }

        /// <summary>
        /// Format a message template with a dictionary.
        /// </summary>
        public static string Format(string template, Dictionary<string, object> values)
        {
            if (string.IsNullOrEmpty(template) || values == null) return template;
            
            foreach (var kvp in values)
            {
                template = template.Replace("{" + kvp.Key + "}", kvp.Value?.ToString() ?? "");
            }
            
            return template;
        }
    }
}
