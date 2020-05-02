using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bot.Config.Localization {
    public class LocalizationPack {
        public string LanguageName { get; set; }
        public string LocalizedName { get; set; }
        public string LocalizationFlagEmoji { get; set; }
        public string FallbackLanguage { get; set; }
        public string Authors { get; set; }
        public Dictionary<string, Dictionary<string, string>> Data { get; set; }
        
        [JsonIgnore] public int TranslationCompleteness { get; set; }
    }
}