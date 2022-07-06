using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Common;
using Discord;
using Discord.WebSocket;
using NLog;

namespace Bot.Utilities.Collector {
    public class CollectorService {
        private readonly EnlivenShardedClient _discordClient;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public CollectorService(EnlivenShardedClient discordClient) {
            _discordClient = discordClient;
            discordClient.ReactionAdded += async (cacheable, channel, arg3) => {
                ReactionAdded.OnNext((cacheable, await channel.GetOrDownloadAsync(), arg3));
            };
            discordClient.MessageReceived += message => {
                MessageReceived.OnNext(message);
                return Task.CompletedTask;
            };
        }

        private Subject<(Cacheable<IUserMessage, ulong> cacheable, IMessageChannel, SocketReaction arg3)> ReactionAdded = new();
        private Subject<SocketMessage> MessageReceived = new();

        public CollectorController CollectReaction(Predicate<SocketReaction> predicate,
                                                   Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            var logger = _logger.WithProperty("Collector registered\n", new StackTrace());
            var collectorController = new CollectorController();

            predicate = ApplyFilters(predicate, filter);
            var disposable = ReactionAdded.Subscribe(tuple => {
                logger.Swallow(() => {
                    if (predicate(tuple.Item3)) action(new EmoteCollectorEventArgs(collectorController, tuple.Item3));
                });
            });
            collectorController.ShouldDispose(disposable);

            return collectorController;
        }

        public CollectorController CollectMessage(Predicate<IMessage> predicate,
                                                  Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            var logger = _logger.WithProperty("Collector registered\n", new StackTrace());
            var collectorController = new CollectorController();

            predicate = ApplyFilters(predicate, filter);
            var disposable = MessageReceived.Subscribe(message => {
                logger.Swallow(() => {
                    if (predicate(message)) action(new MessageCollectorEventArgs(collectorController, message));
                });
            });
            collectorController.ShouldDispose(disposable);

            return collectorController;
        }

        public CollectorController CollectReaction(IChannel channel, Predicate<SocketReaction> predicate,
                                                   Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(channel);
            return CollectReaction(reaction => channel.Id == reaction.Channel.Id && predicate(reaction), action, filter);
        }

        public CollectorController CollectReaction(IEmote emote, Predicate<SocketReaction> predicate,
                                                   Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(emote);
            return CollectReaction(reaction => emote.Equals(reaction.Emote) && predicate(reaction), action, filter);
        }

        public CollectorController CollectReaction(IMessage message, Predicate<SocketReaction> predicate,
                                                   Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(message);
            return CollectReaction(reaction => message.Id == reaction.MessageId && predicate(reaction), action, filter);
        }

        public CollectorController CollectReaction(IUser user, Predicate<SocketReaction> predicate,
                                                   Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(user);
            return CollectReaction(reaction => user.Id == reaction.UserId && predicate(reaction), action, filter);
        }

        public CollectorController CollectMessage(IUser user, Predicate<IMessage> predicate,
                                                  Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(user);
            return CollectMessage(message => user.Id == message.Author.Id && predicate(message), action, filter);
        }

        public CollectorController CollectMessage(IChannel channel, Predicate<IMessage> predicate,
                                                  Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(channel);
            return CollectMessage(message => channel.Id == message.Channel.Id && predicate(message), action, filter);
        }

        public CollectorsGroup CollectReactions<T>(Predicate<SocketReaction> predicate, Action<EmoteMultiCollectorEventArgs, T> action,
                                                   params (IEmote, T)[] selectors) {
            return CollectReactions(predicate, action, selectors.Select(tuple => (tuple.Item1, new Func<T>(() => tuple.Item2))).ToArray());
        }

        public CollectorsGroup CollectReactions<T>(Predicate<SocketReaction> predicate, Action<EmoteMultiCollectorEventArgs, T> action,
                                                   params (IEmote, Func<T>)[] selectors) {
            var logger = _logger.WithProperty("collectorRegistrationStacktrace", $"\nCollector registration\n{new StackTrace(true)}");
            var collectorsGroup = new CollectorsGroup();
            foreach (var selector in selectors.ToList()) {
                var collectorController = new CollectorController();

                var localPredicate = new Predicate<SocketReaction>(reaction => reaction.Emote.Equals(selector.Item1) && predicate(reaction));
                var disposable = ReactionAdded.Subscribe(tuple => {
                    logger.Swallow(() => {
                        if (localPredicate(tuple.Item3))
                            action(new EmoteMultiCollectorEventArgs(collectorController, collectorsGroup, tuple.Item3), selector.Item2());
                    });
                });
                collectorController.ShouldDispose(disposable);

                // ReSharper disable once PossiblyMistakenUseOfParamsMethod
                collectorsGroup.Add(collectorController);
            }

            return collectorsGroup;
        }

        private Predicate<IMessage> ApplyFilters(Predicate<IMessage> initial, CollectorFilter filter) {
            return filter switch {
                CollectorFilter.Off        => initial,
                CollectorFilter.IgnoreSelf => message => message.Author.Id != _discordClient.CurrentUser.Id && initial(message),
                CollectorFilter.IgnoreBots => message => !message.Author.IsBot && !message.Author.IsWebhook && initial(message),
                _                          => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
            };
        }

        private Predicate<SocketReaction> ApplyFilters(Predicate<SocketReaction> initial, CollectorFilter filter) {
            return filter switch {
                CollectorFilter.Off        => initial,
                CollectorFilter.IgnoreSelf => reaction => reaction.UserId != _discordClient.CurrentUser.Id && initial(reaction),
                CollectorFilter.IgnoreBots => reaction => _discordClient.GetUser(reaction.UserId) is { IsBot: false, IsWebhook: false } && initial(reaction),
                _                          => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
            };
        }
    }
}