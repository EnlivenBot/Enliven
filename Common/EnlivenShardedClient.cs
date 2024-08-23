using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace Common;

public class EnlivenShardedClient : DiscordShardedClient
{
    private readonly Subject<SocketInteraction> _interactionCreatedSubject = new();
    private readonly Subject<SocketMessage> _messageReceivedSubject = new();
    private readonly TaskCompletionSource<object> _readyTaskCompletionSource = new();

    public EnlivenShardedClient(IOptions<DiscordSocketConfig> config) : base(config.Value)
    {
        SubscribeToEvents();
        SubscribeToMessageComponents();
    }

    public bool IsReady => Ready.IsCompleted;
    public Task Ready => _readyTaskCompletionSource.Task;
    public IObservable<SocketMessageComponent> MessageComponentUse { get; private set; } = null!;
    public IObservable<SocketInteraction> InteractionCreate { get; private set; } = null!;

    private void SubscribeToEvents()
    {
        ShardReady += OnShardReady;
        InteractionCreated += OnInteractionCreated;
        MessageReceived += OnMessageReceived;
    }

    private Task OnShardReady(DiscordSocketClient client)
    {
        _readyTaskCompletionSource.TrySetResult(true);
        return Task.CompletedTask;
    }

    private Task OnInteractionCreated(SocketInteraction interaction)
    {
        _interactionCreatedSubject.OnNext(interaction);
        return Task.CompletedTask;
    }

    private Task OnMessageReceived(SocketMessage message)
    {
        _messageReceivedSubject.OnNext(message);
        return Task.CompletedTask;
    }

    private void SubscribeToMessageComponents()
    {
        InteractionCreate = _interactionCreatedSubject.AsObservable();
        MessageComponentUse = _interactionCreatedSubject
            .Where(interaction => interaction.Type == InteractionType.MessageComponent)
            .Select(interaction => (SocketMessageComponent)interaction)
            .AsObservable();
    }

    public async Task<IUser?> GetUserAsync(ulong id)
    {
        return GetUser(id) ?? (IUser)await Rest.GetUserAsync(id);
    }

    public async Task<IChannel?> GetChannelAsync(ulong id)
    {
        return GetChannel(id) ?? (IChannel)await Rest.GetChannelAsync(id);
    }

    public async Task<IGuild?> GetGuildAsync(ulong id)
    {
        return GetGuild(id) ?? (IGuild)await Rest.GetGuildAsync(id);
    }
}