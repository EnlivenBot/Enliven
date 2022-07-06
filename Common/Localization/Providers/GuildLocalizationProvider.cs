using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Common.Config;

namespace Common.Localization.Providers {
    public class GuildLocalizationProvider : ILocalizationProvider {
        private GuildConfig _guildConfig;

        public GuildLocalizationProvider(GuildConfig guildConfig) {
            _guildConfig = guildConfig;
            guildConfig.LocalizationChanged.Select(_ => this).Subscribe(_languageChanged);
        }

        public string Get(string id, params object[]? formatArgs) {
            return LocalizationManager.Get(_guildConfig.GetLanguage(), id, formatArgs);
        }

        private readonly ISubject<ILocalizationProvider> _languageChanged = new Subject<ILocalizationProvider>();
        public IObservable<ILocalizationProvider> LanguageChanged => _languageChanged.AsObservable();
    }
}