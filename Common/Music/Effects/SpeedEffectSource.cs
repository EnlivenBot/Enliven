using System;
using System.Threading.Tasks;
using Common.Config;
using Lavalink4NET.Filters;

namespace Common.Music.Effects {
    public class SpeedEffectSource : IPlayerEffectSource {
        public Task<PlayerEffect> CreateEffect(string? args) {
            args = args?.Replace(",", ".");
            float multiplier = 1;
            if (float.TryParse(args, out var parsed)) {
                multiplier = Math.Clamp(parsed, 0.25f, 3f);
            }

            var effect = new PlayerEffect(UserLink.Current, $"Speed {multiplier}x", "Speed") {
                Timescale = new TimescaleFilterOptions() { Speed = multiplier }
            };
            return Task.FromResult(effect);
        }

        public string GetSourceName() {
            return "Speed";
        }
    }
}