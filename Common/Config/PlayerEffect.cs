using System.Collections.Generic;
using System.Collections.Immutable;
using Lavalink4NET.Filters;
using Lavalink4NET.Player;
using LiteDB;

namespace Common.Config {
    public class PlayerEffect : FilterMapBase {
        public PlayerEffect(UserLink user) {
            User = user;
        }

        [BsonId]
        public string Id { get; set; } = null!;

        public UserLink User { get; set; }

        [BsonIgnore]
        public ImmutableDictionary<string, IFilterOptions> CurrentFilters => Filters.ToImmutableDictionary();
    }
}