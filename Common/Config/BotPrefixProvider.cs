namespace Common.Config {
    public class BotPrefixProvider : IPrefixProvider {
        public string GetPrefix() {
            return "&";
        }
    }
}