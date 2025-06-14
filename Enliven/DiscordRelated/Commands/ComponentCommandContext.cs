using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.Commands;

public class ComponentCommandContext : ControllableCommandContext {
    public ComponentCommandContext(IDiscordClient client, IInteractionContext component) : base(client) {
        Component = component;
        User = component.User;
        Channel = component.Channel;
        Guild = component.Guild;
    }

    public IInteractionContext Component { get; }
}