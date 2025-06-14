using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bot.DiscordRelated.Interactions.Handlers;

public class DiscordNetInteractionsHandler(
    CustomInteractionService customInteractionService,
    IServiceProvider serviceProvider,
    ILogger<DiscordNetInteractionsHandler> logger) : IInteractionsHandler
{
    public async ValueTask<IResult?> Handle(IInteractionContext context)
    {
        var interactionSearchResult = SearchInteraction(context);
        if (!interactionSearchResult.IsSuccess)
        {
            logger.LogWarning("Interaction {InteractionText} not found due to {Reason}",
                interactionSearchResult.Text, interactionSearchResult.ErrorReason);
            Activity.Current?.SetStatus(ActivityStatusCode.Error);
            return interactionSearchResult;
        }

        logger.LogDebug("Interaction resolved to {Command}", interactionSearchResult.Command.Name);
        Activity.Current?.SetTag("InteractionCommand", interactionSearchResult.Command.Name);

        return await interactionSearchResult.Command
            .ExecuteAsync(context, serviceProvider)
            .ConfigureAwait(false);
    }

    private SearchResult<ICommandInfo> SearchInteraction(IInteractionContext context)
    {
        return context.Interaction switch
        {
            ISlashCommandInteraction slashCommandInteraction => ParseSearchResultToCommon(
                customInteractionService.SearchSlashCommand(slashCommandInteraction)),
            IComponentInteraction messageComponent => ParseSearchResultToCommon(
                customInteractionService.SearchComponentCommand(messageComponent)),
            IUserCommandInteraction userCommand => ParseSearchResultToCommon(
                customInteractionService.SearchUserCommand(userCommand)),
            IMessageCommandInteraction messageCommand => ParseSearchResultToCommon(
                customInteractionService.SearchMessageCommand(messageCommand)),
            IAutocompleteInteraction autocomplete => ParseSearchResultToCommon(
                customInteractionService.SearchAutocompleteCommand(autocomplete)),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static SearchResult<ICommandInfo> ParseSearchResultToCommon<T>(SearchResult<T> result)
        where T : class, ICommandInfo
    {
        return result.IsSuccess
            ? SearchResult<ICommandInfo>.FromSuccess(result.Text, result.Command, result.RegexCaptureGroups)
            : SearchResult<ICommandInfo>.FromError(result.Text, result.Error!.Value, result.ErrorReason);
    }
}