namespace Bot.Config.Localization.Providers {
    public class LangLocalizationProvider : ILocalizationProvider {
        private readonly string _lang;
        public LangLocalizationProvider(string lang) {
            _lang = lang;
        }

        public string Get(string id) {
            return Localization.Get(_lang, id);
        }

        public string Get(string group, string id) {
            return Localization.Get(_lang, group, id);
        }
    }
}