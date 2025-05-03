using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Common;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bot.Utilities.Collector;

public class CollectorService
{
    private readonly EnlivenShardedClient _discordClient;
    private readonly ILogger<CollectorService> _logger;
    private Subject<SocketMessage> _messageReceived = new();

    private Subject<(Cacheable<IUserMessage, ulong> cacheable, IMessageChannel, SocketReaction arg3)> _reactionAdded =
        new();

    public CollectorService(EnlivenShardedClient discordClient, ILogger<CollectorService> logger)
    {
        _discordClient = discordClient;
        _logger = logger;
        discordClient.ReactionAdded += async (cacheable, channel, arg3) =>
        {
            _reactionAdded.OnNext((cacheable, await channel.GetOrDownloadAsync(), arg3));
        };
        discordClient.MessageReceived += message =>
        {
            _messageReceived.OnNext(message);
            return Task.CompletedTask;
        };
    }

    public CollectorController CollectReaction(Predicate<SocketReaction> predicate,
        Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off)
    {
        var collectorController = new CollectorController();

        predicate = ApplyFilters(predicate, filter);
        var disposable = _reactionAdded.Subscribe(tuple =>
        {
            try
            {
                if (predicate(tuple.Item3)) action(new EmoteCollectorEventArgs(collectorController, tuple.Item3));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while processing reaction interaction callback");
            }
        });
        collectorController.ShouldDispose(disposable);

        return collectorController;
    }

    public CollectorController CollectMessage(Predicate<IMessage> predicate,
        Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off)
    {
        var collectorController = new CollectorController();

        predicate = ApplyFilters(predicate, filter);
        var disposable = _messageReceived.Subscribe(message =>
        {
            try
            {
                if (predicate(message)) action(new MessageCollectorEventArgs(collectorController, message));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while processing message interaction callback");
            }
        });
        collectorController.ShouldDispose(disposable);

        return collectorController;
    }

    public CollectorController CollectReaction(IChannel channel, Predicate<SocketReaction> predicate,
        Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return CollectReaction(reaction => channel.Id == reaction.Channel.Id && predicate(reaction), action, filter);
    }

    public CollectorController CollectReaction(IEmote emote, Predicate<SocketReaction> predicate,
        Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off)
    {
        ArgumentNullException.ThrowIfNull(emote);
        return CollectReaction(reaction => emote.Equals(reaction.Emote) && predicate(reaction), action, filter);
    }

    public CollectorController CollectReaction(IMessage message, Predicate<SocketReaction> predicate,
        Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off)
    {
        ArgumentNullException.ThrowIfNull(message);
        return CollectReaction(reaction => message.Id == reaction.MessageId && predicate(reaction), action, filter);
    }

    public CollectorController CollectReaction(IUser user, Predicate<SocketReaction> predicate,
        Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off)
    {
        ArgumentNullException.ThrowIfNull(user);
        return CollectReaction(reaction => user.Id == reaction.UserId && predicate(reaction), action, filter);
    }

    public CollectorController CollectMessage(IUser user, Predicate<IMessage> predicate,
        Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off)
    {
        ArgumentNullException.ThrowIfNull(user);
        return CollectMessage(message => user.Id == message.Author.Id && predicate(message), action, filter);
    }

    public CollectorController CollectMessage(IChannel channel, Predicate<IMessage> predicate,
        Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return CollectMessage(message => channel.Id == message.Channel.Id && predicate(message), action, filter);
    }

    public CollectorsGroup CollectReactions<T>(Predicate<SocketReaction> predicate,
        Action<EmoteMultiCollectorEventArgs, T> action,
        params (IEmote, T)[] selectors)
    {
        return CollectReactions(predicate, action,
            selectors.Select(tuple => (tuple.Item1, new Func<T>(() => tuple.Item2))).ToArray());
    }

    public CollectorsGroup CollectReactions<T>(Predicate<SocketReaction> predicate,
        Action<EmoteMultiCollectorEventArgs, T> action,
        params (IEmote, Func<T>)[] selectors)
    {
        var collectorsGroup = new CollectorsGroup();
        foreach (var selector in selectors.ToList())
        {
            var collectorController = new CollectorController();

            var localPredicate =
                new Predicate<SocketReaction>(reaction => reaction.Emote.Equals(selector.Item1) && predicate(reaction));
            var disposable = _reactionAdded.Subscribe(tuple =>
            {
                try
                {
                    if (localPredicate(tuple.Item3))
                        action(new EmoteMultiCollectorEventArgs(collectorController, collectorsGroup, tuple.Item3),
                            selector.Item2());
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error while processing reaction interaction callback");
                }
            });
            collectorController.ShouldDispose(disposable);

            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            collectorsGroup.Add(collectorController);
        }

        return collectorsGroup;
    }

    private Predicate<IMessage> ApplyFilters(Predicate<IMessage> initial, CollectorFilter filter)
    {
        return filter switch
        {
            CollectorFilter.Off => initial,
            CollectorFilter.IgnoreSelf => message =>
                message.Author.Id != _discordClient.CurrentUser.Id && initial(message),
            CollectorFilter.IgnoreBots => message =>
                !message.Author.IsBot && !message.Author.IsWebhook && initial(message),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
        };
    }

    private Predicate<SocketReaction> ApplyFilters(Predicate<SocketReaction> initial, CollectorFilter filter)
    {
        return filter switch
        {
            CollectorFilter.Off => initial,
            CollectorFilter.IgnoreSelf => reaction =>
                reaction.UserId != _discordClient.CurrentUser.Id && initial(reaction),
            CollectorFilter.IgnoreBots => reaction =>
                _discordClient.GetUser(reaction.UserId) is { IsBot: false, IsWebhook: false } && initial(reaction),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
        };
    }
}