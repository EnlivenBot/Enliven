using Lavalink4NET;

namespace Bot.Music {
    public class LavalinkNodeInfo {
        public string RestUri { get; set; }
        public string WebSocketUri { get; set; }
        public string Password { get; set; }
        public string Name { get; set; }
        
        public LavalinkNodeOptions ToOptions() {
            return ToOptions(RestUri, WebSocketUri, Password, Name);
        }

        private static int NodeId;
        public static LavalinkNodeOptions ToOptions(string restUri, string webSocketUri, string password, string label) {
            if (string.IsNullOrWhiteSpace(label))
                label = "Node №" + ++NodeId;
            return new LavalinkNodeOptions {
                RestUri = restUri,
                WebSocketUri = webSocketUri,
                Password = password,
                DisconnectOnStop = false,
                Label = label
            };
        }
    }
}