using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace Bot.DiscordRelated.Interactions.Handlers;

/// <summary>
/// Represents a contract for handling interactions within a Discord bot.
/// </summary>
public interface IInteractionsHandler
{
    /// <summary>
    /// Attempts to handle an interaction within a Discord bot.
    /// </summary>
    /// <param name="context">The interaction context containing the relevant information for the current operation.</param>
    /// <returns>The task result contains a result indicating the outcome of the interaction handling or null of not applicable.</returns>
    ValueTask<IResult?> Handle(IInteractionContext context);
}