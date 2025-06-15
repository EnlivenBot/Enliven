using System;
using Bot.DiscordRelated.Interactions.Wrappers;
using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.Commands;

public class ComponentCommandContext : ControllableCommandContext, IEnlivenInteractionContext {

    public ComponentCommandContext(IDiscordClient client, IInteractionContext component) : base(client) {
        if (component.Interaction is not IEnlivenInteraction) {
            throw new ArgumentException("Component.Interaction must be an IEnlivenInteraction");
        }

        Component = component;
        User = component.User;
        Channel = component.Channel;
        Guild = component.Guild;
    }

    public IInteractionContext Component { get; }
    public IDiscordInteraction Interaction => Component.Interaction;

    IEnlivenInteraction IEnlivenInteractionContext.Interaction => (IEnlivenInteraction)Component.Interaction;
}