using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class EnlivenInteractionContextWrapper(IInteractionContext context, IEnlivenInteraction interaction)
    : IInteractionContext, IEnlivenInteractionContext {
    public IDiscordClient Client => context.Client;

    public IGuild Guild => context.Guild;

    public IMessageChannel Channel => context.Channel;

    public IUser User => context.User;
    
    public IEnlivenInteraction Interaction => interaction;

    IDiscordInteraction IInteractionContext.Interaction => interaction;
}