namespace Bot.Config.Localization.Providers {
    public interface ILocalizationProvider {
        string Get(string id);
        string Get(string group, string id);
    }
}