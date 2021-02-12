using Discord.WebSocket;

namespace Bot.Utilities.Collector {
    public class EmoteMultiCollectorEventArgs : EmoteCollectorEventArgs {
        public CollectorsGroup CollectorsGroup { get; set; }
        public EmoteMultiCollectorEventArgs(CollectorController controller, CollectorsGroup group, SocketReaction reaction) : base(controller, reaction) {
            CollectorsGroup = group;
        }
    }
}