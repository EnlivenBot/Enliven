using System;
using System.Linq;
using System.Threading.Tasks;
using Common.Config;
using Common.Localization.Entries;
using Common.Utils;
using Lavalink4NET.Filters;
using Tyrrrz.Extensions;

namespace Common.Music.Effects;

public class BassBoostEffectSource : IPlayerEffectSource
{
    private static readonly string AvailableBassBoostModes =
        Enum.GetValues(typeof(BassBoostMode))
            .Cast<BassBoostMode>()
            .Select(mode => $"`{mode}`")
            .JoinToString(", ");

    private static readonly EntryLocalized ParseBassBoostFailedEntry = new("Music.EffectParseFailedWithDefault",
        AvailableBassBoostModes, nameof(BassBoostMode.Medium));

    public Task<PlayerEffect> CreateEffect(string? args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return Task.FromResult(ConstructBassBoostEffect(BassBoostMode.Medium));
        }

        if (Enum.TryParse(args, true, out BassBoostMode result))
        {
            return Task.FromResult(ConstructBassBoostEffect(result));
        }

        return Task.FromException<PlayerEffect>(new LocalizedException(ParseBassBoostFailedEntry));
    }

    public string GetSourceName()
    {
        return "BassBoost";
    }

    private PlayerEffect ConstructBassBoostEffect(BassBoostMode mode)
    {
        var multiplier = mode switch
        {
            BassBoostMode.Low => 0.15f,
            BassBoostMode.Medium => 0.4f,
            BassBoostMode.High => 0.8f,
            BassBoostMode.Extreme => 1f,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        return new PlayerEffect(UserLink.Current, $"BassBoost ({mode})", "BassBoost")
        {
            Equalizer = new EqualizerFilterOptions(new Equalizer()
            {
                Band0 = 0.65f * multiplier,
                Band1 = 0.85f * multiplier,
                Band2 = 0.45f * multiplier,
                Band3 = 0.20f * multiplier,
                Band4 = 0.10f * multiplier,
                Band5 = 0.05f * multiplier,
            })
        };
    }
}