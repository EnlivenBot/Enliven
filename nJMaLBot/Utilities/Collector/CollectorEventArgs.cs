using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Bot.Utilities.Collector {
    public abstract class CollectorEventArgsBase : EventArgs {
        protected CollectorEventArgsBase(CollectorController controller) {
            Controller = controller;
        }

        public CollectorController Controller { get; set; }

        public void StopCollect() {
            Controller.Dispose();
        }

        public abstract Task RemoveReason();
    }

    public class EmoteCollectorEventArgs : CollectorEventArgsBase {
        public SocketReaction Reaction { get; set; }
        
        public EmoteCollectorEventArgs(CollectorController controller, SocketReaction reaction) : base(controller) {
            Reaction = reaction;
        }

        public override async Task RemoveReason() {
            try {
                var message = (IUserMessage) (Reaction.Message.IsSpecified
                    ? Reaction.Message.Value
                    : await Reaction.Channel.GetMessageAsync(Reaction.MessageId));
                await message.RemoveReactionAsync(Reaction.Emote, Reaction.User.Value);
            }
            catch (Exception) {
                Controller.OnRemoveArgsFailed(this);
            }
        }
    }

    public class MessageCollectorEventArgs : CollectorEventArgsBase {
        public IMessage Message { get; set; }

        public MessageCollectorEventArgs(CollectorController controller, IMessage message) : base(controller) {
            Message = message;
        }

        public override async Task RemoveReason() {
            try {
                await Message.DeleteAsync();
            }
            catch {
                Controller.OnRemoveArgsFailed(this);
            }
        }
    }
}