using System;
using System.Linq;
using System.Threading.Tasks;
using Common.Config;
using Common.Localization.Entries;
using Common.Utils;
using Lavalink4NET.Filters;
using Tyrrrz.Extensions;

namespace Common.Music.Effects {
    public class BassBoostEffectSource : IPlayerEffectSource {
        private static readonly string AvailableBassBoostModes =
            Enum.GetValues(typeof(BassBoostMode))
                .Cast<BassBoostMode>()
                .Select(mode => $"`{mode}`")
                .JoinToString(", ");

        private static EntryLocalized _parseBassBoostFailedEntry = new EntryLocalized("Music.EffectParseFailedWithDefault", AvailableBassBoostModes, nameof(BassBoostMode.Medium));

        public Task<PlayerEffect> CreateEffect(string? args) {
            if (string.IsNullOrWhiteSpace(args)) {
                return Task.FromResult(ConstructBassBoostEffect(BassBoostMode.Medium));
            }

            if (Enum.TryParse(args, true, out BassBoostMode result)) {
                return Task.FromResult(ConstructBassBoostEffect(result));
            }

            return Task.FromException<PlayerEffect>(new LocalizedException(_parseBassBoostFailedEntry));
        }

        public string GetSourceName() {
            return "BassBoost";
        }

        private PlayerEffect ConstructBassBoostEffect(BassBoostMode mode) {
            var multiplier = mode switch {
                BassBoostMode.Low     => 0.15,
                BassBoostMode.Medium  => 0.4,
                BassBoostMode.High    => 0.8,
                BassBoostMode.Extreme => 1,
                _                     => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
            return new PlayerEffect(UserLink.Current, $"BassBoost ({mode})", "BassBoost") {
                Equalizer = new EqualizerFilterOptions() {
                    Bands = PlayerEffect.EffectBassboost.Equalizer!.Bands
                        .Select(band => new EqualizerBand(band.Band, (float)(band.Gain * multiplier)))
                        .ToList()
                }
            };
        }
    }
}