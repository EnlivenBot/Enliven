using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions.Handlers;
using Common;
using Common.Infrastructure.Tracing;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using SerilogTracing.Instrumentation;

namespace Bot.DiscordRelated.Interactions;

public sealed class InteractionsHandlerService(
    CustomInteractionService customInteractionService,
    EnlivenShardedClient enlivenShardedClient,
    IReadOnlyList<IInteractionsHandler> interactionsHandlers,
    ILogger<InteractionsHandlerService> logger)
    : IService, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("Enliven.InteractionHandlerService");
    private IDisposable? _disposable;

    private readonly IReadOnlyList<IInteractionsHandler> _interactionsHandlers =
        interactionsHandlers.Reverse().ToImmutableArray();

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
            IResult? result = null;
            foreach (var interactionsHandler in _interactionsHandlers)
            {
                result = await interactionsHandler.Handle(context);
                if (result is not null)
                {
                    break;
                }
            }

            if (result is null)
            {
                // TODO Respond not found embed and traceid
            }
            else if (!result.IsSuccess)
            {
                var exception = result is ExecuteResult executeResult ? executeResult.Exception : null;
                if (exception is not CommandInterruptionException)
                {
                    logger.LogError(exception, "Interaction failed ({InteractionResult}) due to {Reason}", 
                        result.Error!.Value, result.ErrorReason);
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
}