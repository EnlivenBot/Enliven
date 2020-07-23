using Bot.Config.Localization.Providers;

namespace Bot.Config.Localization.Entries {
    public interface IEntry {
        string Get(ILocalizationProvider provider, params object[] additionalArgs);
    }
}