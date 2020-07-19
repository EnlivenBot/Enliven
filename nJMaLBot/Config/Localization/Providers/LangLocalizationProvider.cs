using System;

namespace Bot.Config.Localization.Providers {
    public class LangLocalizationProvider : ILocalizationProvider {
        private readonly string _lang;
        public LangLocalizationProvider(string lang) {
            _lang = lang;
        }

        public string Get(string id) {
            return LocalizationManager.Get(_lang, id);
        }

        public string Get(string group, string id) {
            return LocalizationManager.Get(_lang, group, id);
        }

        public event EventHandler? LanguageChanged;
    }
}