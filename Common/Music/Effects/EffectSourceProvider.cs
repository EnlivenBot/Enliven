using System.Diagnostics.CodeAnalysis;
using Common.Config;

namespace Common.Music.Effects {
    public class EffectSourceProvider {
        public bool TryResolve(UserLink? link, string effectName, [NotNullWhen(true)] out IPlayerEffectSource? source) {
            if (PlayerEffectSource.DefaultEffectsMap.TryGetValue(effectName, out source)) {
                return true;
            }
            
            // TODO: Implement custom user effects resolving
            return false;
        }
        
        public IPlayerEffectSource? Resolve(UserLink? link, string effectName) {
            return TryResolve(link, effectName, out var effectSource) ? effectSource : null;
        }
    }
}