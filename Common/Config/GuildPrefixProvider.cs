namespace Common.Config {
    public class GuildPrefixProvider : IPrefixProvider {
        private GuildConfig _guildConfig;
        public GuildPrefixProvider(GuildConfig guildConfig) {
            _guildConfig = guildConfig;
        }
        
        public string GetPrefix() {
            return _guildConfig.Prefix;
        }
    }
}