using Discord.WebSocket;

namespace Bot.Utilities.Collector;

public class EmoteMultiCollectorEventArgs : EmoteCollectorEventArgs {
    public EmoteMultiCollectorEventArgs(CollectorController controller, CollectorsGroup group, SocketReaction reaction) : base(controller, reaction) {
        CollectorsGroup = group;
    }
    public CollectorsGroup CollectorsGroup { get; set; }
}