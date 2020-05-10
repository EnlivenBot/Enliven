using Bot.Config.Localization.Providers;

namespace Bot.Config.Localization {
    public class LocalizedEntry {
        public LocalizedEntry(string @group, string id) {
            Group = @group;
            Id = id;
        }

        public LocalizedEntry(string id) : this(id.Split(".")[0], id.Split(".")[1]) { }
        
        public string Group { get; set; }
        public string Id { get; set; }

        public string Get(ILocalizationProvider provider) {
            return provider.Get(Group, Id);
        }
    }
}