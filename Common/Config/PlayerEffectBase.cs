using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Common.Localization.Entries;
using Lavalink4NET.Filters;
using Lavalink4NET.Player;
using LiteDB;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Tyrrrz.Extensions;

namespace Common.Config {
    public class PlayerEffectBase : FilterMapBase {
        public PlayerEffectBase(string displayName, IDictionary<string, IFilterOptions>? filterOptionsMap = null) {
            DisplayName = displayName;
            if (filterOptionsMap != null) {
                Filters = filterOptionsMap.ToDictionary(pair => pair.Key, pair => pair.Value);
            }
        }

        [JsonProperty("Name")]
        public string DisplayName { get; set; }

        [BsonIgnore]
        public ImmutableDictionary<string, IFilterOptions> CurrentFilters => Filters.ToImmutableDictionary();

        public bool IsValid([NotNullWhen(false)] out IEntry? errorEntry) {
            errorEntry = null;
            List<IEntry> errors = new List<IEntry>();

            Verify(!DisplayName.IsNullOrWhiteSpace() && DisplayName?.Length is > 4 and < 15, "NameLength", 4, 15);
            
            Verify(Filters.Count > 0, "FiltersCount");

            // if (Distortion != null) { }
            // if (Karaoke != null) { }
            // if (LowPass != null) { }
            if (Equalizer != null) {
                Verify(Equalizer.Bands.Any(), "ParametersCount", nameof(Equalizer));
                
                Verify(Equalizer.Bands.Any(band => band.Band is < 0 or > 14), "BandsNumbers");
                foreach (var grouping in Equalizer.Bands.GroupBy(band => band.Band).Where(bands => bands.Count() > 1)) {
                    Verify(false, "BandsDuplicate", grouping.Key);
                }
                
                foreach (var equalizerBand in Equalizer.Bands.Where(band => band.Gain is < -0.25f or > 1f)) {
                    Verify(false, "Between", nameof(Equalizer), $"Band {equalizerBand.Band} Gain", "-0.25", "1");
                }
            }

            if (Rotation != null) {
                Verify(Rotation.Frequency is >= -1 and <= 1, "Between", nameof(Rotation), nameof(Rotation.Frequency), -1, 1);
            }

            if (Timescale != null) {
                Verify(Timescale.Speed is >= 0.25f and <= 2, "Between", nameof(Timescale), nameof(Timescale.Speed), 0.25f, 2);
                Verify(Timescale.Pitch is >= 0.25f and <= 2, "Between", nameof(Timescale), nameof(Timescale.Pitch), 0.25f, 2);
                Verify(Timescale.Rate is >= 0.25f and <= 2, "Between", nameof(Timescale), nameof(Timescale.Rate), 0.25f, 2);
            }

            if (Tremolo != null) {
                Verify(Tremolo.Depth is > 0 and <= 1, "Between", nameof(Tremolo), nameof(Tremolo.Depth), 0, 1);
                Verify(Tremolo.Frequency > 0, "MoreThen", nameof(Tremolo), nameof(Tremolo.Frequency), 0);
            }
            
            if (Vibrato != null) {
                Verify(Vibrato.Depth is > 0 and <= 1, "Between", nameof(Vibrato), nameof(Vibrato.Depth), 0, 1);
                Verify(Vibrato.Frequency is > 0 and <= 14, "Between", nameof(Vibrato), nameof(Vibrato.Frequency), 0, 14);
            }

            if (Volume != null) {
                Verify(Volume.Volume is >= 0 and <= 2, "Between", nameof(Volume), nameof(Volume.Volume), 0, 2);
            }

            if (ChannelMix != null) {
                Verify(ChannelMix.LeftToLeft is >= 0 and <= 1, "Between", nameof(ChannelMix), nameof(ChannelMix.LeftToLeft), 0, 1);
                Verify(ChannelMix.LeftToRight is >= 0 and <= 1, "Between", nameof(ChannelMix), nameof(ChannelMix.LeftToRight), 0, 1);
                Verify(ChannelMix.RightToLeft is >= 0 and <= 1, "Between", nameof(ChannelMix), nameof(ChannelMix.RightToLeft), 0, 1);
                Verify(ChannelMix.RightToRight is >= 0 and <= 1, "Between", nameof(ChannelMix), nameof(ChannelMix.RightToRight), 0, 1);
            }

            if (!errors.Any()) return true;
            errorEntry = new EntryString(errors.Select((entry, i) => $"{{{i}}}\n").JoinToString(""), errors.ToArray()); 
            return false;

            void Verify(bool expected, string errorLocId, params object[] errorParams) {
                if (!expected) {
                    errors.Add(new EntryLocalized($"Effects.{errorLocId}", errorParams));
                }
            }
        }

        private static readonly NoConverterContractResolver NoConverterContractResolverInstance = new NoConverterContractResolver();
        public static PlayerEffectBase? FromDefaultJson(string json) {
            return JsonConvert.DeserializeObject<PlayerEffectBase>(json, new JsonSerializerSettings() {ContractResolver = NoConverterContractResolverInstance});
        }

        private sealed class NoConverterContractResolver : DefaultContractResolver
        {
            protected override JsonConverter? ResolveContractConverter(Type objectType)
            {
                return null;
            }
        }
    }
}