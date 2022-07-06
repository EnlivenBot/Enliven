using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Common.Config;
using LiteDB;

namespace Common.Music.Effects {
    public class EffectSourceProvider {
        private readonly IUserDataProvider _userDataProvider;
        public EffectSourceProvider(IUserDataProvider userDataProvider) {
            _userDataProvider = userDataProvider;
        }
        
        public bool TryResolve(UserLink? link, string effectName, [NotNullWhen(true)] out IPlayerEffectSource? source) {
            if (PlayerEffectSource.DefaultEffectsMap.TryGetValue(effectName, out source)) {
                return true;
            }

            var userData = link?.GetData(_userDataProvider);
            var userEffect = userData?.PlayerEffects.LastOrDefault(effect => effect.DisplayName == effectName || effect.SourceName == effectName);
            if (userEffect == null) return false;
            source = new PlayerEffectSource(userEffect);
            return true;
        }
        
        public IPlayerEffectSource? Resolve(UserLink? link, string effectName) {
            return TryResolve(link, effectName, out var effectSource) ? effectSource : null;
        }
    }
}