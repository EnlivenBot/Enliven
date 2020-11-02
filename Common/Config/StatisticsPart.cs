using System.Collections.Concurrent;
using System.Collections.Generic;
using LiteDB;

namespace Common.Config {
    public class StatisticsPart {
        private static ConcurrentDictionary<string, StatisticsPart> _cache = new ConcurrentDictionary<string, StatisticsPart>();

        [BsonId] public string Id { get; set; } = null!;
        public Dictionary<string, int> UsagesList { get; set; } = new Dictionary<string, int>();

        public static StatisticsPart Get(string id) {
            return _cache.GetOrAdd(id, s =>
                Database.CommandStatistics.FindById(id) ?? new StatisticsPart {Id = s}
            );
        }

        public void Save() {
            Database.CommandStatistics.Upsert(this);
        }
    }
}