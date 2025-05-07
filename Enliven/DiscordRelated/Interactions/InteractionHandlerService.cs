using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Bot.Infrastructure.Tracing;
using Common;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SerilogTracing.Instrumentation;

namespace Bot.DiscordRelated.Interactions;

public class InteractionHandlerService : IService, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("Enliven.InteractionHandlerService");
    
    private readonly CustomInteractionService _customInteractionService;
    private readonly EnlivenShardedClient _enlivenShardedClient;
    private readonly ILogger<InteractionHandlerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IDisposable? _disposable;

    public InteractionHandlerService(IServiceProvider serviceProvider,
        CustomInteractionService customInteractionService, EnlivenShardedClient enlivenShardedClient,
        ILogger<InteractionHandlerService> logger)
    {
        _serviceProvider = serviceProvider;
        _customInteractionService = customInteractionService;
        _enlivenShardedClient = enlivenShardedClient;
        _logger = logger;
    }

    public void Dispose()
    {
        _disposable?.Dispose();
        _logger.LogInformation("Interactions disposed");
    }

    public async Task OnDiscordReady()
    {
        await _customInteractionService.RegisterCommandsGloballyAsync();
        _disposable = _enlivenShardedClient.InteractionCreate
            .Select(interaction => new ShardedInteractionContext(_enlivenShardedClient, interaction))
            .Subscribe(context => _ = OnInteractionCreated(context));
        _logger.LogInformation("Interactions initialized");
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
                _logger.LogWarning("Interaction {InteractionText} not found due to {Reason}",
                    interactionSearchResult.Text, interactionSearchResult.ErrorReason);
                activity?.SetStatus(ActivityStatusCode.Error);
                return;
            }
            
            _logger.LogDebug("Interaction successfully resolved to {Command}", interactionSearchResult.Command.Name);

            var result = await interactionSearchResult.Command
                .ExecuteAsync(context, _serviceProvider)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                var exception = result is ExecuteResult executeResult ? executeResult.Exception : null;
                if (exception is not CommandInterruptionException)
                {
                    _logger.LogError(exception, "Interaction {Command} failed ({InteractionResult}) due to {Reason}", 
                        interactionSearchResult.Command.Name, result.Error!.Value, result.ErrorReason);
                    activity?.SetStatus(ActivityStatusCode.Error);
                }
                if (exception is not CommandInterruptionException && exception is not null)
                {
                    activity?.AddException(exception);
                }
            }

            try
            {
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
            _logger.LogError(e, "Error while handling interaction");
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
                _customInteractionService.SearchSlashCommand(slashCommandInteraction)),
            IComponentInteraction messageComponent => ParseSearchResultToCommon(
                _customInteractionService.SearchComponentCommand(messageComponent)),
            IUserCommandInteraction userCommand => ParseSearchResultToCommon(
                _customInteractionService.SearchUserCommand(userCommand)),
            IMessageCommandInteraction messageCommand => ParseSearchResultToCommon(
                _customInteractionService.SearchMessageCommand(messageCommand)),
            IAutocompleteInteraction autocomplete => ParseSearchResultToCommon(
                _customInteractionService.SearchAutocompleteCommand(autocomplete)),
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