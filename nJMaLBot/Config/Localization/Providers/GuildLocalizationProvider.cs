using System;

namespace Bot.Config.Localization.Providers {
    public class GuildLocalizationProvider : ILocalizationProvider {
        private GuildConfig _guildConfig;

        public GuildLocalizationProvider(ulong guildId) : this(GuildConfig.Get(guildId)) { }

        public GuildLocalizationProvider(GuildConfig guildConfig) {
            _guildConfig = guildConfig;
            GuildConfig.LocalizationChanged += (sender, s) => {
                if (sender is GuildConfig config && config.GuildId == _guildConfig.GuildId) {
                    _guildConfig = config;
                    OnLanguageChanged();
                }
            };
        }

        public string Get(string id) {
            return LocalizationManager.Get(_guildConfig.GetLanguage(), id);
        }

        public string Get(string group, string id) {
            return LocalizationManager.Get(_guildConfig.GetLanguage(), group, id);
        }

        public event EventHandler? LanguageChanged;

        protected virtual void OnLanguageChanged() {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}