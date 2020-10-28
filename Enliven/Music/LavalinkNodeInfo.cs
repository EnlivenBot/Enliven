using Lavalink4NET;

namespace Bot.Music {
    public class LavalinkNodeInfo {
        public string RestUri { get; set; } = null!;
        public string WebSocketUri { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Name { get; set; } = null!;

        public LavalinkNodeOptions ToOptions() {
            return ToOptions(RestUri, WebSocketUri, Password, Name);
        }

        private static int _nodeId;
        public static LavalinkNodeOptions ToOptions(string restUri, string webSocketUri, string password, string label) {
            if (string.IsNullOrWhiteSpace(label))
                label = "Node №" + ++_nodeId;
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