using Discord;

namespace Bot.DiscordRelated.Interactions.Wrappers;

public interface IEnlivenInteractionContext : IInteractionContext
{
    new IEnlivenInteraction Interaction { get; }
}