using System;
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

        public abstract void RemoveReason();
    }

    public class EmoteCollectorEventArgs : CollectorEventArgsBase {
        public SocketReaction Reaction { get; set; }
        
        public EmoteCollectorEventArgs(CollectorController controller, SocketReaction reaction) : base(controller) {
            Reaction = reaction;
        }

        public override void RemoveReason() {
            try {
                Reaction.Message.Value.RemoveReactionAsync(Reaction.Emote, Reaction.User.Value);
            }
            catch (Exception) {
                // ignored
            }
        }
    }

    public class MessageCollectorEventArgs : CollectorEventArgsBase {
        public IMessage Message { get; set; }

        public MessageCollectorEventArgs(CollectorController controller, IMessage message) : base(controller) {
            Message = message;
        }

        public override void RemoveReason() {
            Message.SafeDelete();
        }
    }
}