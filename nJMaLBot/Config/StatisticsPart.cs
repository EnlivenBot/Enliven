using System;
using System.Collections.Generic;
using LiteDB;

namespace Bot.Config {
    public class StatisticsPart {
        [BsonId] public string Id { get; set; }
        public Dictionary<string, int> UsagesList { get; set; } = new Dictionary<string, int>();
    }
    
    [Obsolete("Class deprecated, left for backward compability. Use StatisticsPart")]
    public class ObsoleteStatisticsPart {
        [BsonId] public string Id { get; set; }
        public Dictionary<string, long> UsagesList { get; set; } = new Dictionary<string, long>();
    }
}