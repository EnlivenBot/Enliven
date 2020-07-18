using Bot.Config.Localization.Providers;

namespace Bot.Config.Localization.Entries {
    public interface IEntryLocalized {
        string Get(ILocalizationProvider provider, params object[] additionalArgs);
    }
}