using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions.Wrappers;
using Common;
using Common.Utils;
using Discord;
using Microsoft.Extensions.Logging;

namespace Bot.DiscordRelated.UpdatableMessage;

public class UpdatableMessageDisplay : DisposableBase {
    private static readonly TimeSpan ResendDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan InteractionBasedUpdateDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MessageBasedUpdateDelay = TimeSpan.FromSeconds(3);

    private readonly List<IUpdatableMessageDisplayBehavior> _behaviors = [];
    private readonly ILogger? _logger;
    private readonly Action<MessageProperties> _messagePropertiesUpdateCallback;
    private readonly SingleTask<Unit, IEnlivenInteraction?> _controlMessageSendTask;
    private readonly SingleTask<Unit, IEnlivenInteraction?> _updateControlMessageTask;
    private InteractionMessageHolder? _interaction;
    private Subject<InteractionMessageHolder>? _messageChangedSubject;

    public bool UpdateViaInteractions => _interaction?.InteractionAvailable() ?? false;
    public IMessageChannel TargetChannel { get; }

    public IObservable<InteractionMessageHolder> MessageChanged =>
        (_messageChangedSubject ??= new Subject<InteractionMessageHolder>()).AsObservable();

    public UpdatableMessageDisplay(IMessageChannel targetChannel,
        Action<MessageProperties> messagePropertiesUpdateCallback,
        ILogger? logger) : this(targetChannel,
        messagePropertiesUpdateCallback, [], logger) {
    }

    public UpdatableMessageDisplay(IMessageChannel targetChannel,
        Action<MessageProperties> messagePropertiesUpdateCallback,
        IEnumerable<IUpdatableMessageDisplayBehavior> behaviors,
        ILogger? logger) {
        TargetChannel = targetChannel;
        _messagePropertiesUpdateCallback = messagePropertiesUpdateCallback;
        _logger = logger;
        _controlMessageSendTask = new SingleTask<Unit, IEnlivenInteraction?>(SendControlMessageInternal)
            { BetweenExecutionsDelay = ResendDelay, CanBeDirty = false };
        _updateControlMessageTask = new SingleTask<Unit, IEnlivenInteraction?>(UpdateControlMessageInternal) {
            BetweenExecutionsDelay = MessageBasedUpdateDelay, CanBeDirty = true,
            ShouldExecuteNonDirtyIfNothingRunning = true
        };

        foreach (var behavior in behaviors) {
            AttachBehavior(behavior);
        }
    }

    private async Task<Unit> SendControlMessageInternal(SingleTaskExecutionData<IEnlivenInteraction?> data) {
        _ = _interaction?.DeleteAsync().ObserveException();

        var requestOptions = new RequestOptions {
            CancelToken = data.CancellationToken,
            RetryMode = RetryMode.AlwaysFail,
            RatelimitCallback = RateLimitCallback
        };

        var messageProperties = new MessageProperties();
        _messagePropertiesUpdateCallback(messageProperties);
        if (data.Parameter is { } interaction) {
            await interaction.RespondAsync(text: messageProperties.Content.GetValueOrDefault(),
                embed: messageProperties.Embed.GetValueOrDefault(),
                embeds: messageProperties.Embeds.GetValueOrDefault(),
                components: messageProperties.Components.GetValueOrDefault(),
                options: requestOptions);
            OnInteractionProcessed(InteractionMessageHolder.CreateFromInteraction(interaction, OnInteractionExpired));
            return Unit.Default;
        }

        var message = await TargetChannel.SendMessageAsync(text: messageProperties.Content.GetValueOrDefault(),
            embed: messageProperties.Embed.GetValueOrDefault(),
            embeds: messageProperties.Embeds.GetValueOrDefault(),
            components: messageProperties.Components.GetValueOrDefault(),
            options: requestOptions);
        OnInteractionProcessed(InteractionMessageHolder.CreateFromMessage(message));
        return Unit.Default;

        Task RateLimitCallback(IRateLimitInfo info) {
            if (info.RetryAfter is { } retryAfter) {
                data.OverrideDelay = retryAfter > data.BetweenExecutionsDelay.GetValueOrDefault().TotalSeconds
                    ? TimeSpan.FromSeconds(retryAfter + 1)
                    : null;
            }

            return Task.CompletedTask;
        }
    }

    private async Task<Unit> UpdateControlMessageInternal(SingleTaskExecutionData<IEnlivenInteraction?> data) {
        await _controlMessageSendTask.WaitForCurrent().ObserveException();
        if (_interaction == null) {
            await _controlMessageSendTask.ExecuteForcedIfHasArgument(data.Parameter);
            return Unit.Default;
        }

        var requestOptions = new RequestOptions {
            CancelToken = data.CancellationToken,
            RetryMode = RetryMode.AlwaysFail,
            RatelimitCallback = RateLimitCallback
        };

        // Try to resend an interaction response
        // TODO: Handle already responded interactions
        if (data.Parameter is { HasResponded: false } interaction) {
            // If new interaction is a component interaction, and it's the same message as the control message
            // Just update it
            if (interaction is IComponentInteraction componentInteraction) {
                if (componentInteraction.Message.Id == await _interaction.GetMessageIdAsync()) {
                    await componentInteraction.UpdateAsync(_messagePropertiesUpdateCallback, requestOptions);
                    var holder = InteractionMessageHolder.CreateFromComponentInteraction(componentInteraction,
                        OnInteractionExpired);
                    OnInteractionProcessed(holder);
                    return Unit.Default;
                }
            }

            // Other types of interactions, but main message sent recently
            // TODO: Handle resending
            if (_interaction is not null
                && DateTimeOffset.Now - _interaction.InteractionCreatedAt < TimeSpan.FromSeconds(10)) {
                // Just defer it
                await interaction.DeferAsync(options: requestOptions);
                // Delete loading message
                _ = interaction.DeleteOriginalResponseAsync().ObserveException();
                return Unit.Default;
            }

            await _controlMessageSendTask.ForcedExecute(interaction);
            return Unit.Default;
        }

        if (await ShouldResend()) {
            await _controlMessageSendTask.Execute();
            return Unit.Default;
        }

        try {
            await _interaction.ModifyAsync(_messagePropertiesUpdateCallback, requestOptions);
        }
        catch (TimeoutException) {
            // Ignore all timeouts because weird Discord (Discord.NET?) logic.
            // Fuck it.
        }
        catch (Exception e) {
            _logger?.LogTrace(e, "Failed to update embed control message. " +
                                 "Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}",
                (TargetChannel as IGuildChannel)?.GuildId, TargetChannel.Id, await _interaction.GetMessageIdAsync());

            _ = _controlMessageSendTask.Execute();
        }

        return Unit.Default;

        Task RateLimitCallback(IRateLimitInfo info) {
            if (info.RetryAfter is { } retryAfter) {
                data.OverrideDelay = retryAfter > data.BetweenExecutionsDelay.GetValueOrDefault().TotalSeconds
                    ? TimeSpan.FromSeconds(retryAfter + 1)
                    : null;
            }

            return Task.CompletedTask;
        }
    }

    public void AttachBehavior(IUpdatableMessageDisplayBehavior behavior) {
        behavior.OnAttached(this);
        _behaviors.Add(behavior);
    }

    public Task Update(bool background) {
        return _updateControlMessageTask.Execute(makesDirty: !background);
    }

    public Task HandleInteraction(InteractionMessageHolder holder) {
        if (_interaction is null) {
            OnInteractionProcessed(holder);
            return _updateControlMessageTask.Execute();
        }

        if (holder.InteractionAvailable()) {
            if (!_interaction.InteractionAvailable()
                || _interaction.InteractionAvailable()
                && holder.InteractionCreatedAt > _interaction.InteractionCreatedAt) {
                _ = _interaction.DeleteAsync().ObserveException();
                OnInteractionProcessed(holder);
                return _updateControlMessageTask.Execute();
            }
        }

        _ = holder.DeleteAsync().ObserveException();
        return _updateControlMessageTask.Execute();
    }

    public Task HandleInteraction(IEnlivenInteraction interaction) {
        return _updateControlMessageTask.ForcedExecute(interaction);
    }

    private void OnInteractionProcessed(InteractionMessageHolder interaction) {
        _updateControlMessageTask.BetweenExecutionsDelay = interaction.InteractionAvailable()
            ? InteractionBasedUpdateDelay
            : MessageBasedUpdateDelay;
        _interaction = interaction;
        _messageChangedSubject?.OnNext(interaction);
    }

    private void OnInteractionExpired() {
        _updateControlMessageTask.BetweenExecutionsDelay = MessageBasedUpdateDelay;
    }

    private async ValueTask<bool> ShouldResend() {
        if (_behaviors.Count == 0) {
            return false;
        }

        List<ValueTask<bool>>? tasks = null;
        foreach (var behavior in _behaviors.OfType<IUpdatableMessageDisplayResendBehavior>()) {
            tasks ??= new List<ValueTask<bool>>(_behaviors.Count);
            tasks.Add(behavior.ShouldResend());
        }

        if (tasks is null) {
            return false;
        }

        foreach (var valueTask in tasks) {
            if (await valueTask) {
                return true;
            }
        }

        return false;
    }

    public new InteractionMessageHolder? Dispose() {
        base.Dispose();
        return Interlocked.Exchange(ref _interaction, null);
    }

    protected override void DisposeInternal() {
        _controlMessageSendTask.Dispose();
        _updateControlMessageTask.Dispose();
        _messageChangedSubject?.Dispose();
        foreach (var behavior in _behaviors) {
            behavior.Dispose();
        }
    }
}