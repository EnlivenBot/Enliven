using Common.Config;
using Discord;

namespace Common.Music {
    public class PlayerEffectUse {
        public PlayerEffectUse(IUser? user, PlayerEffect effect) {
            User = user;
            Effect = effect;
        }
        
        public IUser? User { get; }
        public PlayerEffect Effect { get; }

        public override string ToString() {
            return $"{Effect.DisplayName} by {User?.Username}";
        }
    }
}