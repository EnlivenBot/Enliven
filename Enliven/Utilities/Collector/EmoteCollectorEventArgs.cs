using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Bot.Utilities.Collector;

public class EmoteCollectorEventArgs : CollectorEventArgsBase {
    public EmoteCollectorEventArgs(CollectorController controller, SocketReaction reaction) : base(controller) {
        Reaction = reaction;
    }
    public SocketReaction Reaction { get; set; }

    public override async Task RemoveReason() {
        try {
            var message = (IUserMessage)(Reaction.Message.IsSpecified
                ? Reaction.Message.Value
                : await Reaction.Channel.GetMessageAsync(Reaction.MessageId));
            await message.RemoveReactionAsync(Reaction.Emote, Reaction.User.Value);
        }
        catch (Exception) {
            Controller.OnRemoveArgsFailed(this);
        }
    }
}