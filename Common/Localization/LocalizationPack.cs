using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Discord;
using Newtonsoft.Json;

namespace Common.Localization {
    public class LocalizationPack {
        private string _localizationFlagEmojiText = null!;
        public string LanguageName { get; set; } = null!;
        public string LocalizedName { get; set; } = null!;

        [JsonProperty("LocalizationFlagEmoji")]
        public string LocalizationFlagEmojiText {
            get => _localizationFlagEmojiText;
            set {
                _localizationFlagEmojiText = value;
                try {
                    LocalizationFlagEmoji = new Emoji(value);
                }
                catch (Exception) {
                    // ignored
                }
            }
        }

        [JsonIgnore]
        public IEmote? LocalizationFlagEmoji { get; set; }

        public string FallbackLanguage { get; set; } = null!;
        public string Authors { get; set; } = null!;
        public Dictionary<string, Dictionary<string, string>> Data { get; set; } = null!;

        [JsonIgnore] public int TranslationCompleteness { get; set; }

        public List<string> GetLocalizationEntries() {
            return Data.SelectMany(groups => groups.Value.Select(pair => groups.Key + "." + pair.Key))
                .Where(s => !s.Contains("._")) // Remove all auxiliary strings
                .ToList();
        }

        public void CalcTranslationCompleteness(List<string> entries) {
            var entriesNotLocalizedCount = GetLocalizationEntries().Count(entries.Contains);
            TranslationCompleteness = (int) (entriesNotLocalizedCount / (double) entries.Count * 100);
        }

        public bool TryGetEntry(string group, string id, [NotNullWhen(true)] out string? value) {
            value = null;
            if (!Data.TryGetValue(group, out var reqGroup) || !reqGroup.TryGetValue(id, out var reqText)) return false;
            value = reqText;
            return true;
        }
    }
}