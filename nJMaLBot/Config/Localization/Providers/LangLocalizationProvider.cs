using System;

namespace Bot.Config.Localization.Providers {
    public class LangLocalizationProvider : ILocalizationProvider {
        private readonly string _lang;
        public LangLocalizationProvider(string lang) {
            _lang = lang;
        }

        public string Get(string id, params object[]? formatArgs) {
            return LocalizationManager.Get(_lang, id, formatArgs);
        }

        public event EventHandler? LanguageChanged;
    }
}