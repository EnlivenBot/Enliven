using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.Commands {
    public class ReactionCommandContext : ControllableCommandContext {
        public ReactionCommandContext(IDiscordClient client, SocketReaction reaction) : base(client) {
            Reaction = reaction;
            User = reaction.User.GetValueOrDefault(EnlivenBot.Client.GetUser(reaction.UserId));
            Channel = reaction.Channel;
            if (reaction.Channel is SocketTextChannel channel)
                Guild = channel.Guild;
        }

        public SocketReaction Reaction { get; set; }
    }
}