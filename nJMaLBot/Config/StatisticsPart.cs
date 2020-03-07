using System.Collections.Generic;
using LiteDB;

namespace Bot.Config {
    public class StatisticsPart {
        [BsonId] public string Id { get; set; }
        public Dictionary<string, ulong> UsagesList { get; set; } = new Dictionary<string, ulong>();
    }
}