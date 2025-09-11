using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions.Wrappers;
using Common;
using Common.Utils;
using Discord;
using Microsoft.Extensions.Logging;

namespace Bot.DiscordRelated.Music;

public class UpdatableMessageDisplay : DisposableBase {
    private static readonly TimeSpan ResendDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InteractionBasedUpdateDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MessageBasedUpdateDelay = TimeSpan.FromSeconds(3);

    private readonly ILogger _logger;
    private readonly IMessageChannel _targetChannel;
    private readonly Action<MessageProperties> _messagePropertiesUpdateCallback;
    private readonly SingleTask<Unit, IEnlivenInteraction?> _controlMessageSendTask;
    private readonly SingleTask<Unit, IEnlivenInteraction?> _updateControlMessageTask;
    private InteractionMessageHolder? _interaction;

    public bool UpdateViaInteractions => _interaction?.InteractionAvailable() ?? false;

    public UpdatableMessageDisplay(IMessageChannel targetChannel,
        Action<MessageProperties> messagePropertiesUpdateCallback, ILogger logger) {
        _targetChannel = targetChannel;
        _messagePropertiesUpdateCallback = messagePropertiesUpdateCallback;
        _logger = logger;
        _controlMessageSendTask = new SingleTask<Unit, IEnlivenInteraction?>(SendControlMessageInternal)
            { BetweenExecutionsDelay = ResendDelay, CanBeDirty = false };
        _updateControlMessageTask = new SingleTask<Unit, IEnlivenInteraction?>(UpdateControlMessageInternal) {
            BetweenExecutionsDelay = MessageBasedUpdateDelay, CanBeDirty = true,
            ShouldExecuteNonDirtyIfNothingRunning = true
        };
    }

    private async Task<Unit> SendControlMessageInternal(SingleTaskExecutionData<IEnlivenInteraction?> arg) {
        _ = _interaction?.DeleteAsync().ObserveException();

        var messageProperties = new MessageProperties();
        _messagePropertiesUpdateCallback(messageProperties);
        if (arg.Parameter is { } interaction) {
            await interaction.RespondAsync(text: messageProperties.Content.GetValueOrDefault(),
                messageProperties.Embeds.GetValueOrDefault([]),
                components: messageProperties.Components.GetValueOrDefault());
            OnInteractionProcessed(InteractionMessageHolder.CreateFromInteraction(interaction, OnInteractionExpired));
            return Unit.Default;
        }

        var message = await _targetChannel.SendMessageAsync(text: messageProperties.Content.GetValueOrDefault(),
            embeds: [messageProperties.Embed.GetValueOrDefault()],
            components: messageProperties.Components.GetValueOrDefault());
        _interaction = InteractionMessageHolder.CreateFromMessage(message);
        return Unit.Default;
    }

    private async Task<Unit> UpdateControlMessageInternal(SingleTaskExecutionData<IEnlivenInteraction?> data) {
        await _controlMessageSendTask.WaitForCurrent().ObserveException();
        if (_interaction == null) {
            await _controlMessageSendTask.Execute();
            return Unit.Default;
        }

        var requestOptions = new RequestOptions {
            CancelToken = data.CancellationToken,
            RetryMode = RetryMode.AlwaysFail,
            RatelimitCallback = RateLimitCallback
        };

        // Try to resend an interaction response
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
            if (_interaction is not null &&
                DateTimeOffset.Now - _interaction.CreatedAt < TimeSpan.FromSeconds(10)) {
                // Just defer it
                await interaction.DeferAsync(options: requestOptions);
                // Delete loading message
                _ = interaction.DeleteOriginalResponseAsync().ObserveException();
                return Unit.Default;
            }

            await _controlMessageSendTask.ForcedExecute(interaction);
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
            _logger.LogTrace(e, "Failed to update embed control message. " +
                                "Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}",
                (_targetChannel as IGuildChannel)?.GuildId, _targetChannel.Id, await _interaction.GetMessageIdAsync());

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

    public Task Update(bool background) {
        return _updateControlMessageTask.Execute(makesDirty: !background);
    }

    public Task HandleInteraction(IEnlivenInteraction interaction) {
        return _updateControlMessageTask.ForcedExecute(interaction);
    }

    private void OnInteractionProcessed(InteractionMessageHolder interaction) {
        _updateControlMessageTask.BetweenExecutionsDelay = InteractionBasedUpdateDelay;
        _interaction = interaction;
    }

    private void OnInteractionExpired() {
        _updateControlMessageTask.BetweenExecutionsDelay = MessageBasedUpdateDelay;
    }

    public new InteractionMessageHolder? Dispose() {
        base.Dispose();
        return Interlocked.Exchange(ref _interaction, null);
    }

    protected override void DisposeInternal() {
        _controlMessageSendTask.Dispose();
        _updateControlMessageTask.Dispose();
    }

    public class InteractionMessageHolder {
        private bool _interactionWasValid;
        private readonly IEnlivenInteraction? _interaction;
        private readonly Task<IUserMessage> _controlMessageTask;
        private readonly Action _interactionExpiredCallback;

        private InteractionMessageHolder(IEnlivenInteraction? interaction,
            Task<IUserMessage> controlMessageTask,
            Action interactionExpiredCallback) {
            _interaction = interaction;
            _controlMessageTask = controlMessageTask;
            _interactionExpiredCallback = interactionExpiredCallback;
            _interactionWasValid = interaction is not null;
        }

        public DateTimeOffset? CreatedAt => _interaction?.CreatedAt;

        public async Task ModifyAsync(Action<MessageProperties> messagePropertiesUpdateCallback,
            RequestOptions? requestOptions = null) {
            if (InteractionAvailable()) {
                await _interaction!.ModifyOriginalResponseAsync(messagePropertiesUpdateCallback, requestOptions);
            }
            else {
                if (_interactionWasValid) {
                    _interactionWasValid = false;
                    _interactionExpiredCallback();
                }

                await (await _controlMessageTask).ModifyAsync(messagePropertiesUpdateCallback, requestOptions);
            }
        }

        public async Task DeleteAsync() {
            if (InteractionAvailable())
                await _interaction!.DeleteOriginalResponseAsync();
            else
                await (await _controlMessageTask).DeleteAsync();
        }

        public ValueTask<ulong> GetMessageIdAsync() {
            return _controlMessageTask.IsCompletedSuccessfully
                ? ValueTask.FromResult(_controlMessageTask.Result.Id)
                : new ValueTask<ulong>(_controlMessageTask.PipeAsync(message => message.Id));
        }

        public bool InteractionAvailable() {
            return _interaction is not null && DateTimeOffset.Now - _interaction.CreatedAt < new TimeSpan(0, 14, 50);
        }

        public static InteractionMessageHolder CreateFromInteraction(IEnlivenInteraction interaction,
            Action interactionExpiredCallback) {
            return new InteractionMessageHolder(interaction, interaction.GetOriginalResponseAsync(),
                interactionExpiredCallback);
        }

        public static InteractionMessageHolder CreateFromComponentInteraction(IComponentInteraction interaction,
            Action interactionExpiredCallback) {
            return new InteractionMessageHolder((IEnlivenInteraction?)interaction,
                Task.FromResult(interaction.Message),
                interactionExpiredCallback);
        }

        public static InteractionMessageHolder CreateFromMessage(IUserMessage controlMessage) {
            return new InteractionMessageHolder(null, Task.FromResult(controlMessage), () => { });
        }
    }
}