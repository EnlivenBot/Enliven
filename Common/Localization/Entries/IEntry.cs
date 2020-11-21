using Common.Localization.Providers;

namespace Common.Localization.Entries {
    public interface IEntry {
        string Get(ILocalizationProvider provider, params object[] additionalArgs);
    }
}