using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Common;
using Common.Infrastructure.Tracing;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SerilogTracing.Instrumentation;

namespace Bot.DiscordRelated.Interactions;

public sealed class InteractionHandlerService(
    IServiceProvider serviceProvider,
    CustomInteractionService customInteractionService,
    EnlivenShardedClient enlivenShardedClient,
    ILogger<InteractionHandlerService> logger)
    : IService, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("Enliven.InteractionHandlerService");
    private IDisposable? _disposable;

    public void Dispose()
    {
        _disposable?.Dispose();
        logger.LogInformation("Interactions disposed");
    }

    public async Task OnDiscordReady()
    {
        await customInteractionService.RegisterCommandsGloballyAsync();
        _disposable = enlivenShardedClient.InteractionCreate
            .Select(interaction => new ShardedInteractionContext(enlivenShardedClient, interaction))
            .Subscribe(context => _ = OnInteractionCreated(context));
        logger.LogInformation("Interactions initialized");
    }

    private async Task OnInteractionCreated(ShardedInteractionContext context)
    {
        Activity.Current = null;
        using var activity = ActivitySource.StartActivityStructured(ActivityKind.Server, 
            "Received discord interaction from {User} ({UserId}) in {GuildId}-{ChannelId}", 
            context.User.Username, context.User.Id, context.Guild?.Id, context.Channel.Id);
        activity?.AddTag("InteractionType", GetInteractionType(context));
        try
        {
            var interactionSearchResult = SearchInteraction(context);
            if (!interactionSearchResult.IsSuccess)
            {
                logger.LogWarning("Interaction {InteractionText} not found due to {Reason}",
                    interactionSearchResult.Text, interactionSearchResult.ErrorReason);
                activity?.SetStatus(ActivityStatusCode.Error);
                return;
            }
            
            logger.LogDebug("Interaction resolved to {Command}", interactionSearchResult.Command.Name);
            activity?.SetTag("InteractionCommand", interactionSearchResult.Command.Name);

            var result = await interactionSearchResult.Command
                .ExecuteAsync(context, serviceProvider)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                var exception = result is ExecuteResult executeResult ? executeResult.Exception : null;
                if (exception is not CommandInterruptionException)
                {
                    logger.LogError(exception, "Interaction {Command} failed ({InteractionResult}) due to {Reason}", 
                        interactionSearchResult.Command.Name, result.Error!.Value, result.ErrorReason);
                    activity?.SetStatus(ActivityStatusCode.Error);
                }
                if (exception is not CommandInterruptionException && exception is not null)
                {
                    activity?.AddException(exception);
                }
                
                // TODO[#12]: Reply with traceid
            }

            try
            {
                // TODO[#13]: Expose InteractionsModuleContext here to avoid discord roundtrip
                var restInteractionMessage = await context.Interaction.GetOriginalResponseAsync();
                if (restInteractionMessage is not null && (restInteractionMessage.Flags & MessageFlags.Loading) != 0)
                    await restInteractionMessage.DeleteAsync();
            }
            catch (Exception)
            {
                // ignored
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while handling interaction");
            if (activity is not null)
            {
                ActivityInstrumentation.TrySetException(activity, e);
                activity.SetStatus(ActivityStatusCode.Error);
            }
        }
    }
    
    private string GetInteractionType(ShardedInteractionContext context)
    {
        return context.Interaction switch
        {
            ISlashCommandInteraction => "ISlashCommandInteraction",
            IComponentInteraction => "IComponentInteraction",
            IUserCommandInteraction => "IUserCommandInteraction",
            IMessageCommandInteraction => "IMessageCommandInteraction",
            IAutocompleteInteraction => "IAutocompleteInteraction",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private SearchResult<ICommandInfo> SearchInteraction(ShardedInteractionContext context)
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