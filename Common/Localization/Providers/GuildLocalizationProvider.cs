using System;
using System.Reactive.Subjects;
using Common.Config;

namespace Common.Localization.Providers {
    public class GuildLocalizationProvider : ILocalizationProvider {
        private GuildConfig _guildConfig;

        public GuildLocalizationProvider(GuildConfig guildConfig) {
            LanguageChanged = new Subject<ILocalizationProvider>();
            _guildConfig = guildConfig;
            guildConfig.LocalizationChanged.Subscribe(config => LanguageChanged.OnNext(this));
        }

        public string Get(string id, params object[]? formatArgs) {
            return LocalizationManager.Get(_guildConfig.GetLanguage(), id, formatArgs);
        }

        public ISubject<ILocalizationProvider> LanguageChanged { get; }
    }
}