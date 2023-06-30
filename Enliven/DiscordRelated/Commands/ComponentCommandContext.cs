using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.Commands;

public class ComponentCommandContext : ControllableCommandContext {
    public ComponentCommandContext(IDiscordClient client, SocketMessageComponent component) : base(client) {
        Component = component;
        User = component.User;
        Channel = component.Channel;
        if (component.Channel is SocketTextChannel channel)
            Guild = channel.Guild;
    }
    public SocketMessageComponent Component { get; }
}