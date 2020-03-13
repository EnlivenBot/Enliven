using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Utilities.Modules {
    public class EmojiCommandContext : ICommandContext {
        public IDiscordClient Client { get; }
        public IGuild Guild { get; }
        public IMessageChannel Channel { get; }
        public IUser User { get; }
        public IUserMessage Message { get; } = null;
        public SocketReaction Reaction { get; }
        public EmojiCommandContext(IDiscordClient client, SocketReaction reaction) {
            Client = client;
            Reaction = reaction;
            User = reaction.User.Value;
            Channel = reaction.Message.Value.Channel;
            Guild = ((SocketTextChannel) reaction.Message.Value.Channel).Guild;
        }
    }
}