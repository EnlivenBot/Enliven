using System;
using System.Collections.Generic;
using Discord;
using Newtonsoft.Json;

namespace Bot.Config.Localization {
    public class LocalizationPack {
        private string _localizationFlagEmojiText;
        public string LanguageName { get; set; }
        public string LocalizedName { get; set; }

        [JsonProperty("LocalizationFlagEmoji")]
        public string LocalizationFlagEmojiText {
            get => _localizationFlagEmojiText;
            set {
                _localizationFlagEmojiText = value;
                try {
                     LocalizationFlagEmoji = new Emoji(value);
                }
                catch (Exception e) {
                    // ignored
                }
            }
        }

        [JsonIgnore]
        public IEmote LocalizationFlagEmoji { get; set; }
        public string FallbackLanguage { get; set; }
        public string Authors { get; set; }
        public Dictionary<string, Dictionary<string, string>> Data { get; set; }
        
        [JsonIgnore] public int TranslationCompleteness { get; set; }
    }
}