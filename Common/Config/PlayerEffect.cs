using System.Collections.Generic;
using System.Collections.Immutable;
using Lavalink4NET.Filters;
using Lavalink4NET.Player;
using LiteDB;

namespace Common.Config {
    public partial class PlayerEffect : FilterMapBase {
        public PlayerEffect(UserLink user, string name) {
            User = user;
            Name = name;
            
            Filters = new Dictionary<string, IFilterOptions>();
        }

        [BsonId]
        public string Id { get; set; } = null!;

        public string Name { get; set; }

        public UserLink User { get; set; }

        [BsonIgnore]
        public ImmutableDictionary<string, IFilterOptions> CurrentFilters => Filters.ToImmutableDictionary();
    }

    public partial class PlayerEffect {
        public static PlayerEffect Effect8D { get; } = new PlayerEffect(UserLink.Current, "8d") {
            Rotation = new RotationFilterOptions() {Frequency = 0.125f}
        };
        
        public static PlayerEffect EffectNightcore { get; } = new PlayerEffect(UserLink.Current, "nightcore") {
            Timescale = new TimescaleFilterOptions() {Rate = 1.2f, Speed = 1.2f}
        };
        
        public static PlayerEffect EffectBassboost { get; } = new PlayerEffect(UserLink.Current, "bassboost") {
            Equalizer = new EqualizerFilterOptions() {
                Bands = new[] {
                    new EqualizerBand(0, 0.55f),
                    new EqualizerBand(1, 0.85f),
                    new EqualizerBand(2, 0.65f),
                    new EqualizerBand(3, 0.45f),
                    new EqualizerBand(4, 0.35f),
                    new EqualizerBand(5, 0.25f)
                }
            }
        };

        public static PlayerEffect EffectMono { get; } = new PlayerEffect(UserLink.Current, "mono") {
            ChannelMix = new ChannelMixFilterOptions() {
                LeftToLeft = 0.5f,
                LeftToRight = 0.5f,
                RightToLeft = 0.5f,
                RightToRight = 0.5f
            }
        };
        
        public static ImmutableDictionary<string, PlayerEffect> PredefinedEffects { get; } =
            new Dictionary<string, PlayerEffect>() {
                {"8d", Effect8D },
                {"nightcore", EffectNightcore },
                {"nc", EffectNightcore },
                {"bassboost", EffectBassboost },
                {"bb", EffectBassboost },
                {"mono", EffectMono },
            }.ToImmutableDictionary();
    }
}