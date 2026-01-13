namespace TankRequest.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Root state containing all user data and queues.
    /// Serialized to JSON and stored in Streamer.bot global variable "tq.state".
    /// </summary>
    public class LedgerState
    {
        public Dictionary<string, UserState> users { get; set; } = new Dictionary<string, UserState>();
        public List<QueueItem> supporterQueue { get; set; } = new List<QueueItem>();
        public List<QueueItem> normalQueue { get; set; } = new List<QueueItem>();
    }

    /// <summary>
    /// Per-user state containing token buckets.
    /// </summary>
    public class UserState
    {
        public string userName { get; set; } = "";
        public List<Bucket> buckets { get; set; } = new List<Bucket>();
    }

    /// <summary>
    /// A token bucket with amount and expiration.
    /// </summary>
    public class Bucket
    {
        public int amount { get; set; }
        public DateTime expiresAtUtc { get; set; }
        public string source { get; set; } = "";
    }

    /// <summary>
    /// A queue item representing a tank request.
    /// </summary>
    public class QueueItem
    {
        public string user { get; set; } = "";
        public string tank { get; set; } = "";
        public int mult { get; set; } = 1;
        public DateTime tsUtc { get; set; } = DateTime.UtcNow;
        public string raw { get; set; } = "";
        public string tipAmount { get; set; } = "";
        public string redemptionId { get; set; } = "";
        public string rewardId { get; set; } = "";
    }
}
