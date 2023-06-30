using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Attributes;
using Discord;
using Discord.Interactions;

namespace Bot.DiscordRelated.Commands.Modules.Contexts;

public class InteractionsModuleContext : ICommonModuleContext, IInteractionContext {
    private Func<ICommandInfo> _commandResolver;
    private Timer? _deferSendTimer;
    private bool _isLoadingDeleted;
    public InteractionsModuleContext(IInteractionContext originalContext, Func<ICommandInfo> commandResolver) {
        _commandResolver = commandResolver;
        OriginalContext = originalContext;
    }
    public IInteractionContext OriginalContext { get; }

    public bool HasLoadingSent { get; private set; }

    public IDiscordClient Client => OriginalContext.Client;
    public IGuild Guild => OriginalContext.Guild;
    public IMessageChannel Channel => OriginalContext.Channel;
    public IUser User => OriginalContext.User;

    public bool NeedResponse => !Interaction.HasResponded;
    public bool HasMeaningResponseSent { get; private set; }
    public bool CanSendEphemeral => true;

    public ValueTask BeforeExecuteAsync() {
        var delayBeforeLoading = DateTime.Now - (Interaction.CreatedAt + TimeSpan.FromSeconds(2));
        if (delayBeforeLoading <= TimeSpan.Zero)
            DeferIfNeeded();
        else
            _deferSendTimer = new Timer(_ => DeferIfNeeded(), null, delayBeforeLoading, TimeSpan.MaxValue);

        return ValueTask.CompletedTask;
    }

    public async ValueTask AfterExecuteAsync() {
        if (_deferSendTimer != null) await _deferSendTimer.DisposeAsync();
        if (!HasLoadingSent && !HasMeaningResponseSent) await DeferIfNeeded();
        if (HasLoadingSent && !HasMeaningResponseSent) {
            _isLoadingDeleted = true;
            var restInteractionMessage = await Interaction.GetOriginalResponseAsync();
            if ((restInteractionMessage.Flags & MessageFlags.Loading) != 0) await restInteractionMessage.DeleteAsync();
        }
    }

    public async Task<SentMessage> SendMessageAsync(string? text, Embed[]? embeds, bool ephemeral = false, MessageComponent? components = null) {
        var hasMeaningResponseSent = HasMeaningResponseSent;
        HasMeaningResponseSent = true;
        // No loading and no responded
        if (!HasLoadingSent && !hasMeaningResponseSent) {
            await Interaction.RespondAsync(text, embeds, ephemeral: ephemeral, components: components);
            return new SentMessage(() => Interaction.GetOriginalResponseAsync(), ephemeral);
        }
        // Only loading sent
        if (HasLoadingSent && !hasMeaningResponseSent && !_isLoadingDeleted) {
            var message0 = await Interaction.ModifyOriginalResponseAsync(properties => {
                properties.Content = text ?? Optional<string>.Unspecified;
                properties.Components = components ?? Optional<MessageComponent>.Unspecified;
                properties.Embeds = embeds ?? Optional<Embed[]>.Unspecified;
            });
            return new SentMessage(message0, false);
        }
        var message1 = await Interaction.FollowupAsync(text, embeds, ephemeral: ephemeral, components: components);
        return new SentMessage(message1, ephemeral);
    }
    public IDiscordInteraction Interaction => OriginalContext.Interaction;

    private Task DeferIfNeeded() {
        _deferSendTimer?.Dispose();
        if (Interaction.CreatedAt + TimeSpan.FromSeconds(3) >= DateTimeOffset.Now) return Task.CompletedTask;
        if (HasLoadingSent || HasMeaningResponseSent) return Task.CompletedTask;
        HasLoadingSent = true;
        return Interaction.DeferAsync();
    }

    private static bool NeedLoadingSend(ICommandInfo commandInfo) {
        var longRunningAttribute = commandInfo.Attributes.OfType<LongRunningCommandAttribute>().FirstOrDefault()
                                ?? commandInfo.Module.Attributes.OfType<LongRunningCommandAttribute>().FirstOrDefault();
        return longRunningAttribute?.IsLongRunning ?? false;
    }
}