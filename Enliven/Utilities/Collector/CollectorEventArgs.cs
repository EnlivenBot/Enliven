using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Common;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Utilities.Collector {
    public abstract class CollectorEventArgsBase : EventArgs {
        protected CollectorEventArgsBase(CollectorController controller) {
            Controller = controller;
            Controller.TaskCompletionSource?.SetResult(this);
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

    public class EmoteMultiCollectorEventArgs : EmoteCollectorEventArgs {
        public CollectorsGroup CollectorsGroup { get; set; }
        public EmoteMultiCollectorEventArgs(CollectorController controller, CollectorsGroup group, SocketReaction reaction) : base(controller, reaction) {
            CollectorsGroup = group;
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

    public class CommandCollectorEventArgs : CollectorEventArgsBase {
        public bool Handled { get; set; }
        
        public IMessage Message { get; private set; }
        public CommandMatch CommandInfo { get; private set; }
        public ParseResult ParseResult { get; private set; }
        public ICommandContext Context { get; private set; }

        public CommandCollectorEventArgs(CollectorController controller, IMessage message, KeyValuePair<CommandMatch, ParseResult> info,
                                         ICommandContext context) : base(controller) {
            Message = message;
            CommandInfo = info.Key;
            Context = context;
            ParseResult = info.Value;
        }

        public override Task RemoveReason() {
            Message.SafeDelete();
            return Task.CompletedTask;
        }

        public async Task<IResult> ExecuteCommand(ICommandContext? overrideContext = null) {
            return await CommandInfo.ExecuteAsync(overrideContext ?? Context, ParseResult, EmptyServiceProvider.Instance).ConfigureAwait(false);
        }
    }
}