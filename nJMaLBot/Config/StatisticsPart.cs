using System.Collections.Generic;
using LiteDB;

namespace Bot.Config {
    public class StatisticsPart {
        [BsonId] public string Id { get; set; } = null!;
        public Dictionary<string, int> UsagesList { get; set; } = new Dictionary<string, int>();
    }
}