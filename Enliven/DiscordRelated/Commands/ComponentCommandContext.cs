using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.Commands;

public class ComponentCommandContext : ControllableCommandContext {
    public ComponentCommandContext(IDiscordClient client, SocketMessageComponent component) : base(client) {
        Component = component;
        User = component.User;
        if (component is SocketMessageComponent socketMessageComponent)
        {
            Channel = socketMessageComponent.Channel;
            if (socketMessageComponent.Channel is SocketTextChannel channel)
                Guild = channel.Guild;
        }
    }
    public SocketMessageComponent Component { get; }
}