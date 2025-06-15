using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions.Handlers;
using Bot.DiscordRelated.Interactions.Wrappers;
using Common;
using Common.Infrastructure.Tracing;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SerilogTracing.Instrumentation;

namespace Bot.DiscordRelated.Interactions;

public sealed class InteractionsHandlerService(
    CustomInteractionService customInteractionService,
    EnlivenShardedClient enlivenShardedClient,
    IReadOnlyList<IInteractionsHandler> interactionsHandlers,
    ILogger<InteractionsHandlerService> logger)
    : IService, IDisposable {
    private static readonly ActivitySource _activitySource = new("Enliven.InteractionHandlerService");
    private IDisposable? _disposable;

    private readonly IReadOnlyList<IInteractionsHandler> _interactionsHandlers =
        interactionsHandlers.Reverse().ToImmutableArray();

    public void Dispose() {
        _disposable?.Dispose();
        logger.LogInformation("Interactions disposed");
    }

    public async Task OnDiscordReady() {
        await customInteractionService.RegisterCommandsGloballyAsync();
        _disposable = enlivenShardedClient.InteractionCreate
            .Select(interaction => new ShardedInteractionContext(enlivenShardedClient, interaction))
            .Subscribe(context => _ = OnInteractionCreated(context));
        logger.LogInformation("Interactions initialized");
    }

    private async Task OnInteractionCreated(ShardedInteractionContext context) {
        Activity.Current = null;
        using var activity = _activitySource.StartActivityStructured(ActivityKind.Server,
            "Received discord interaction from {User} ({UserId}) in {GuildId}-{ChannelId}",
            context.User.Username, context.User.Id, context.Guild?.Id, context.Channel.Id);
        activity?.AddTag("InteractionType", GetInteractionType(context));

        var wrappedContext = new EnlivenInteractionContextWrapper(context, WrapInteraction(context.Interaction));
        SetupAutoDefer(wrappedContext, activity);

        try {
            IResult? result = null;
            foreach (var interactionsHandler in _interactionsHandlers) {
                result = await interactionsHandler.Handle(wrappedContext);
                if (result is not null) {
                    break;
                }
            }

            if (result is null) {
                // TODO Respond not found embed and traceid
                return;
            }

            if (!result.IsSuccess) {
                var exception = result is ExecuteResult executeResult ? executeResult.Exception : null;
                if (exception is not CommandInterruptionException) {
                    logger.LogError(exception, "Interaction failed ({InteractionResult}) due to {Reason}",
                        result.Error!.Value, result.ErrorReason);
                    activity?.SetStatus(ActivityStatusCode.Error);
                }

                if (exception is not CommandInterruptionException && exception is not null) {
                    activity?.AddException(exception);
                }

                // TODO[#12]: Reply with traceid
                return;
            }

            await wrappedContext.Interaction.RemoveDeferredMessageIfNeeded().ObserveException();
        }
        catch (Exception e) {
            logger.LogError(e, "Error while handling interaction");
            if (activity is not null) {
                ActivityInstrumentation.TrySetException(activity, e);
                activity.SetStatus(ActivityStatusCode.Error);
            }
        }
    }

    private void SetupAutoDefer(EnlivenInteractionContextWrapper context, Activity? activity) {
        var delayBeforeLoading = context.Interaction.CreatedAt + TimeSpan.FromSeconds(2) - DateTimeOffset.Now;
        if (delayBeforeLoading <= TimeSpan.Zero) {
            _ = DeferIfNeeded();
            return;
        }

        _ = Task.Delay(delayBeforeLoading).ContinueWith(_ => DeferIfNeeded());

        async Task DeferIfNeeded() {
            if (context.Interaction.HasResponded) return;
            if (DateTimeOffset.Now > context.Interaction.CreatedAt + TimeSpan.FromSeconds(2.9)) return;
            var previousActivity = Activity.Current;
            Activity.Current = activity;
            try {
                await context.Interaction.DeferAsync();
            }
            catch (Exception e) {
                logger.LogWarning(e, "Error while deferring interaction");
            }
            finally {
                Activity.Current = previousActivity;
            }
        }
    }

    private IEnlivenInteraction WrapInteraction(IDiscordInteraction interaction) {
        // @formatter:off
        return interaction switch
        {
            IMessageCommandInteraction messageCommandInteraction => new MessageCommandInteractionWrapper(messageCommandInteraction),
            ISlashCommandInteraction slashCommandInteraction => new SlashCommandInteractionWrapper(slashCommandInteraction),
            IUserCommandInteraction userCommandInteraction => new UserCommandInteractionWrapper(userCommandInteraction),
            IAutocompleteInteraction autocompleteInteraction => new AutocompleteInteractionWrapper(autocompleteInteraction),
            IComponentInteraction componentInteraction => new ComponentInteractionWrapper(componentInteraction),
            _ => throw new ArgumentOutOfRangeException(nameof(interaction))
        };
        // @formatter:on
    }

    private string GetInteractionType(ShardedInteractionContext context) {
        return context.Interaction switch {
            ISlashCommandInteraction => "ISlashCommandInteraction",
            IComponentInteraction => "IComponentInteraction",
            IUserCommandInteraction => "IUserCommandInteraction",
            IMessageCommandInteraction => "IMessageCommandInteraction",
            IAutocompleteInteraction => "IAutocompleteInteraction",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}