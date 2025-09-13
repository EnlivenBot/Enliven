using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Bot.DiscordRelated.UpdatableMessage;

public sealed class StayInTheViewUpdatableMessageDisplayBehavior(BaseSocketClient discordClient, int messageCount)
    : IUpdatableMessageDisplayResendBehavior {
    private readonly HashSet<ulong> _messageIds = [];
    private IDisposable? _disposable;

    public void OnAttached(UpdatableMessageDisplay display) {
        discordClient.MessageReceived += OnMessageReceived;
        discordClient.MessageDeleted += OnMessageDeleted;
        _disposable = display.MessageChanged
            .DistinctUntilNewMessage()
            .Subscribe(OnDisplayMessageChanged);
    }

    private void OnDisplayMessageChanged(ulong messageId) {
        _messageIds.Clear();
    }

    private Task OnMessageReceived(SocketMessage message) {
        _messageIds.Add(message.Id);
        return Task.CompletedTask;
    }

    private Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel) {
        _messageIds.Remove(message.Id);
        return Task.CompletedTask;
    }

    public ValueTask<bool> ShouldResend() {
        return ValueTask.FromResult(_messageIds.Count >= messageCount);
    }

    public void Dispose() {
        discordClient.MessageReceived -= OnMessageReceived;
        discordClient.MessageDeleted -= OnMessageDeleted;
        _disposable?.Dispose();
    }
}