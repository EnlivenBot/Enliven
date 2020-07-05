using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Utilities.Modules {
    public class ControllableCommandContext : ICommandContext {
        public IDiscordClient Client { get; set; }
        public IGuild? Guild { get; set; }
        public IMessageChannel? Channel { get; set; }
        public IUser? User { get; set; }
        public IUserMessage? Message { get; }

        public ControllableCommandContext(IDiscordClient client, IUserMessage message) {
            Client = client;
            Message = message;
            User = message.Author;
            Channel = message.Channel;
            Guild = ((SocketTextChannel) Channel).Guild;
        }

        public ControllableCommandContext(IDiscordClient client) {
            Client = client;
        }
    }

    public class ReactionCommandContext : ControllableCommandContext {
        public ReactionCommandContext(IDiscordClient client, SocketReaction reaction) : base(client) {
            Client = client;
            Reaction = reaction;
            User = reaction.User.GetValueOrDefault(Program.Client.GetUser(reaction.UserId));
            Channel = reaction.Channel;
            if (reaction.Channel is SocketTextChannel channel)
                Guild = channel.Guild;
        }

        public SocketReaction Reaction { get; set; }
    }
}