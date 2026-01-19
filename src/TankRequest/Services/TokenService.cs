namespace TankRequest.Services
{
    using System;
    using System.Linq;
    using TankRequest.Models;

    /// <summary>
    /// Manages token operations: balance calculation, consumption, and crediting.
    /// </summary>
    public class TokenService
    {
        private readonly Config _config;

        public TokenService(Config config)
        {
            _config = config;
        }

        /// <summary>
        /// Calculate active (non-expired) token balance for a user.
        /// </summary>
        public int GetActiveBalance(UserState user)
        {
            var now = DateTime.UtcNow;
            return user.buckets
                .Where(b => b.expiresAtUtc > now)
                .Sum(b => b.amount);
        }

        /// <summary>
        /// Get the earliest expiration time for active tokens.
        /// </summary>
        public DateTime? GetNextExpiry(UserState user)
        {
            var now = DateTime.UtcNow;
            var active = user.buckets.Where(b => b.expiresAtUtc > now && b.amount > 0);
            return active.Any() ? active.Min(b => b.expiresAtUtc) : (DateTime?)null;
        }

        /// <summary>
        /// Consume tokens from user's buckets (FIFO - oldest first).
        /// Returns true if successful, false if insufficient balance.
        /// </summary>
        public bool Consume(UserState user, int amount)
        {
            if (GetActiveBalance(user) < amount)
                return false;

            var now = DateTime.UtcNow;
            int remaining = amount;

            // Sort by expiry (soonest first) and consume
            var activeBuckets = user.buckets
                .Where(b => b.expiresAtUtc > now && b.amount > 0)
                .OrderBy(b => b.expiresAtUtc)
                .ToList();

            foreach (var bucket in activeBuckets)
            {
                if (remaining <= 0) break;
                int take = Math.Min(bucket.amount, remaining);
                bucket.amount -= take;
                remaining -= take;
            }

            // Remove empty buckets
            user.buckets.RemoveAll(b => b.amount <= 0);
            return true;
        }

        /// <summary>
        /// Credit tokens to user with expiration.
        /// </summary>
        public void Credit(UserState user, int amount, string source)
        {
            user.buckets.Add(new Bucket
            {
                amount = amount,
                expiresAtUtc = DateTime.UtcNow.AddHours(_config.TtlHours),
                source = source
            });
        }

        /// <summary>
        /// Remove expired buckets from user state.
        /// </summary>
        public void PurgeExpired(UserState user)
        {
            var now = DateTime.UtcNow;
            user.buckets.RemoveAll(b => b.expiresAtUtc <= now);
        }

        /// <summary>
        /// Remove specified amount of tokens (for testing/manual adjustment).
        /// Returns actual amount removed.
        /// </summary>
        public int Remove(UserState user, int amount)
        {
            int balance = GetActiveBalance(user);
            int toRemove = Math.Min(amount, balance);
            
            if (toRemove <= 0) return 0;
            
            Consume(user, toRemove);
            return toRemove;
        }

        /// <summary>
        /// Calculate tokens to award based on event type.
        /// </summary>
        public int CalculateTokens(string eventSource, string eventType, int tier = 0, int bits = 0, decimal tipAmount = 0, int giftCount = 1)
        {
            if (eventSource == "Twitch")
            {
                switch (eventType.ToLower())
                {
                    case "subscription":
                    case "resub":
                    case "giftsub":
                        return GetTierTokens(tier);
                    case "giftbomb":
                        return GetTierTokens(tier) * giftCount;
                    case "cheer":
                        return _config.BitsPerToken > 0 ? bits / _config.BitsPerToken : 0;
                }
            }
            else if (eventSource == "StreamElements")
            {
                if (eventType.ToLower() == "tip")
                    return _config.TipPerToken > 0 ? (int)(tipAmount / _config.TipPerToken) : 0;
            }

            return 0;
        }

        private int GetTierTokens(int tier)
        {
            switch (tier)
            {
                case 1: return _config.Tier1Tokens;
                case 2: return _config.Tier2Tokens;
                case 3: return _config.Tier3Tokens;
                default: return _config.Tier1Tokens;
            }
        }
    }
}
