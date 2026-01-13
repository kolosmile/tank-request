namespace TankRequest.Services
{
    using System.Text.RegularExpressions;
    using TankRequest.Models;

    /// <summary>
    /// Manages queue operations: add, remove, parse input.
    /// </summary>
    public class QueueService
    {
        private readonly Config _config;

        public QueueService(Config config)
        {
            _config = config;
        }

        /// <summary>
        /// Parse raw input string to extract tank name, cost, and special type.
        /// Example: "IS-7 x3" -> ("IS-7", 3, "Normal", null)
        /// Example: "Arty xA" -> ("Arty", 5, "Arty", null)
        /// </summary>
        public (string tank, int cost, string type, string error) ParseInput(string raw, bool forceMult1 = false)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return ("", 1, "Normal", "Adj meg egy tanknevet! Pl.: 'IS-7'");

            raw = raw.Trim();
            int cost = 1;
            string type = "Normal";
            string tank = raw;

            // Check for special modifiers first
            var matchSpecial = Regex.Match(raw, @"^(.+?)\s*[xX]([aAfFtT])$");
            if (matchSpecial.Success)
            {
                tank = matchSpecial.Groups[1].Value.Trim();
                string modifier = matchSpecial.Groups[2].Value.ToUpper();
                
                if (modifier == "A") { cost = _config.CostArty; type = "Arty"; }
                else if (modifier == "F") { cost = _config.CostBlacklist; type = "Blacklist"; }
                else if (modifier == "T") { cost = _config.CostTroll; type = "Troll"; }
            }
            else
            {
                // Check for multiplier pattern: x2, x3, etc.
                var matchMult = Regex.Match(raw, @"^(.+?)\s*[xX](\d+)$");
                if (matchMult.Success)
                {
                    tank = matchMult.Groups[1].Value.Trim();
                    if (!forceMult1)
                        int.TryParse(matchMult.Groups[2].Value, out cost);
                }
            }

            if (string.IsNullOrWhiteSpace(tank))
                return ("", 1, "Normal", "Adj meg egy tanknevet! Pl.: 'IS-7'");

            if (tank.Length > _config.MaxTankNameLength)
                return ("", 1, "Normal", $"Túl hosszú a tanknév (max {_config.MaxTankNameLength} karakter)!");

            return (tank, cost, type, null);
        }

        /// <summary>
        /// Add item to supporter queue.
        /// </summary>
        public void AddToSupporterQueue(LedgerState state, QueueItem item)
        {
            state.supporterQueue.Add(item);
        }

        /// <summary>
        /// Add item to normal queue.
        /// </summary>
        public void AddToNormalQueue(LedgerState state, QueueItem item)
        {
            state.normalQueue.Add(item);
        }

        /// <summary>
        /// Remove and return top item from queue (supporter first, then normal).
        /// Returns null if both queues are empty.
        /// </summary>
        public (QueueItem item, bool isSupporter) DequeueTop(LedgerState state)
        {
            if (state.supporterQueue.Count > 0)
            {
                var item = state.supporterQueue[0];
                state.supporterQueue.RemoveAt(0);
                return (item, true);
            }
            if (state.normalQueue.Count > 0)
            {
                var item = state.normalQueue[0];
                state.normalQueue.RemoveAt(0);
                return (item, false);
            }
            return (null, false);
        }

        /// <summary>
        /// Remove and return top normal queue item only.
        /// Returns null if queue is empty or supporter is on top.
        /// </summary>
        public QueueItem RefundTopNormal(LedgerState state)
        {
            if (state.supporterQueue.Count > 0)
                return null; // Supporter on top, can't refund

            if (state.normalQueue.Count == 0)
                return null;

            var item = state.normalQueue[0];
            state.normalQueue.RemoveAt(0);
            return item;
        }

        /// <summary>
        /// Clear and return all normal queue items for mass refund.
        /// </summary>
        public QueueItem[] RefundAllNormal(LedgerState state)
        {
            var items = state.normalQueue.ToArray();
            state.normalQueue.Clear();
            return items;
        }
    }
}
