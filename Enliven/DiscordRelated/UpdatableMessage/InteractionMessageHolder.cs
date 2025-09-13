using System;
using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions.Wrappers;
using Common;
using Discord;

namespace Bot.DiscordRelated.UpdatableMessage;

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

    public DateTimeOffset? InteractionCreatedAt => _interaction?.CreatedAt;

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