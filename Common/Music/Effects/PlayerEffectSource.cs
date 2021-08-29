using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Common.Config;
using Lavalink4NET.Filters;

namespace Common.Music.Effects {
    public partial class PlayerEffectSource : IPlayerEffectSource {
        private PlayerEffect _effect;
        
        public PlayerEffectSource(PlayerEffect effect) {
            _effect = effect;
        }
        
        public Task<PlayerEffect> CreateEffect(string? args) {
            return Task.FromResult(_effect);
        }
        
        public string GetSourceName() {
            return _effect.SourceName;
        }
    }
    
    public partial class PlayerEffectSource {
        public static IPlayerEffectSource Effect8D { get; } = new PlayerEffectSource(PlayerEffect.Effect8D);

        public static IPlayerEffectSource EffectNightcore { get; } = new PlayerEffectSource(PlayerEffect.EffectNightcore);

        public static IPlayerEffectSource EffectBassboost { get; } = new BassBoostEffectSource();

        public static IPlayerEffectSource EffectMono { get; } = new PlayerEffectSource(PlayerEffect.EffectMono);

        public static IPlayerEffectSource EffectSpeed { get; } = new SpeedEffectSource();
        
        public static ImmutableDictionary<string, IPlayerEffectSource> DefaultEffectsMap { get; } 
            = new Dictionary<string, IPlayerEffectSource>() {
                {"8d", Effect8D },
                {"nightcore", EffectNightcore },
                {"nc", EffectNightcore },
                {"bassboost", EffectBassboost },
                {"bb", EffectBassboost },
                {"mono", EffectMono },
                {"speed", EffectSpeed },
            }.ToImmutableDictionary();

        public static ImmutableList<IPlayerEffectSource> DefaultEffects { get; } 
            = DefaultEffectsMap.Values.Distinct().ToImmutableList();
    }
}