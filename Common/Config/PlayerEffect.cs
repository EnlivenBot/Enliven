using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Lavalink4NET.Filters;
using Lavalink4NET.Player;
using LiteDB;

namespace Common.Config {
    public partial class PlayerEffect : FilterMapBase {
        public PlayerEffect(UserLink user, string displayName) : this(user, displayName, displayName) { }
        public PlayerEffect(UserLink user, string displayName, string sourceName) {
            User = user;
            DisplayName = displayName;
            SourceName = sourceName;

            Filters = new Dictionary<string, IFilterOptions>();
        }

        [BsonId]
        public string Id { get; set; } = null!;

        public string DisplayName { get; set; }

        public string SourceName { get; set; }

        public UserLink User { get; set; }

        [BsonIgnore]
        public ImmutableDictionary<string, IFilterOptions> CurrentFilters => Filters.ToImmutableDictionary();
    }

    public partial class PlayerEffect {
        public static PlayerEffect Effect8D { get; } = new PlayerEffect(UserLink.Current, "8D") {
            Rotation = new RotationFilterOptions() {Frequency = 0.125f}
        };

        public static PlayerEffect EffectNightcore { get; } = new PlayerEffect(UserLink.Current, "Nightcore") {
            Timescale = new TimescaleFilterOptions() {Rate = 1.2f, Speed = 1.2f}
        };

        public static PlayerEffect EffectBassboost { get; } = new PlayerEffect(UserLink.Current, "Bassboost") {
            Equalizer = new EqualizerFilterOptions() {
                Bands = new[] {
                    new EqualizerBand(0, 0.65f),
                    new EqualizerBand(1, 0.85f),
                    new EqualizerBand(2, 0.45f),
                    new EqualizerBand(3, 0.20f),
                    new EqualizerBand(4, 0.10f),
                    new EqualizerBand(5, 0.05f)
                }
            }
        };

        public static PlayerEffect EffectMono { get; } = new PlayerEffect(UserLink.Current, "Mono") {
            ChannelMix = new ChannelMixFilterOptions() {
                LeftToLeft = 0.5f,
                LeftToRight = 0.5f,
                RightToLeft = 0.5f,
                RightToRight = 0.5f
            }
        };
    }
}