using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Common;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;

#pragma warning disable 1998

namespace Bot.Utilities.Collector {
    public static class CollectorsUtils {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static Subject<(Cacheable<IUserMessage, ulong>, ISocketMessageChannel, SocketReaction)> ReactionAdded =
            new Subject<(Cacheable<IUserMessage, ulong>, ISocketMessageChannel, SocketReaction)>();

        private static Subject<SocketMessage> MessageReceived = new Subject<SocketMessage>();

        static CollectorsUtils() {
            EnlivenBot.Client.ReactionAdded += (cacheable, channel, arg3) => {
                ReactionAdded.OnNext((cacheable, channel, arg3));
                return Task.CompletedTask;
            };
            EnlivenBot.Client.MessageReceived += message => {
                MessageReceived.OnNext(message);
                return Task.CompletedTask;
            };
        }

        public static CollectorController CollectReaction(Predicate<SocketReaction> predicate,
                                                          Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            var collectorController = new CollectorController();

            predicate = ApplyFilters(predicate, filter);
            var disposable = ReactionAdded.Subscribe(tuple => {
                logger.Swallow(() => {
                    if (predicate(tuple.Item3)) action(new EmoteCollectorEventArgs(collectorController, tuple.Item3));
                });
            });
            collectorController.Stop += (sender, args) => disposable.Dispose();

            return collectorController;
        }

        public static CollectorController CollectMessage(Predicate<IMessage> predicate,
                                                         Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            var collectorController = new CollectorController();

            predicate = ApplyFilters(predicate, filter);
            var disposable = MessageReceived.Subscribe(message => {
                logger.Swallow(() => {
                    if (predicate(message)) action(new MessageCollectorEventArgs(collectorController, message));
                });
            });
            collectorController.Stop += (sender, args) => disposable.Dispose();

            return collectorController;
        }

        public static CollectorController CollectReaction(IChannel channel, Predicate<SocketReaction> predicate,
                                                          Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(channel);
            return CollectReaction(reaction => channel.Id == reaction.Channel.Id && predicate(reaction), action, filter);
        }

        public static CollectorController CollectReaction(IEmote emote, Predicate<SocketReaction> predicate,
                                                          Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(emote);
            return CollectReaction(reaction => emote.Equals(reaction.Emote) && predicate(reaction), action, filter);
        }

        public static CollectorController CollectReaction(IMessage message, Predicate<SocketReaction> predicate,
                                                          Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(message);
            return CollectReaction(reaction => message.Id == reaction.MessageId && predicate(reaction), action, filter);
        }

        public static CollectorController CollectReaction(IUser user, Predicate<SocketReaction> predicate,
                                                          Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(user);
            return CollectReaction(reaction => user.Id == reaction.UserId && predicate(reaction), action, filter);
        }

        public static CollectorController CollectMessage(IUser user, Predicate<IMessage> predicate,
                                                         Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(user);
            return CollectMessage(message => user.Id == message.Author.Id && predicate(message), action, filter);
        }

        public static CollectorController CollectMessage(IChannel channel, Predicate<IMessage> predicate,
                                                         Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            Assert.NotNull(channel);
            return CollectMessage(message => channel.Id == message.Channel.Id && predicate(message), action, filter);
        }

        public static CollectorsGroup CollectReactions<T>(Predicate<SocketReaction> predicate, Action<EmoteMultiCollectorEventArgs, T> action,
                                                          params (IEmote, T)[] selectors) {
            return CollectReactions(predicate, action, selectors.Select(tuple => (tuple.Item1, new Func<T>(() => tuple.Item2))).ToArray());
        }

        public static CollectorsGroup CollectReactions<T>(Predicate<SocketReaction> predicate, Action<EmoteMultiCollectorEventArgs, T> action,
                                                          params (IEmote, Func<T>)[] selectors) {
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
                collectorController.Stop += (sender, args) => disposable.Dispose();

                // ReSharper disable once PossiblyMistakenUseOfParamsMethod
                collectorsGroup.Add(collectorController);
            }

            return collectorsGroup;
        }

        #region Collect commands

        private static ConcurrentDictionary<CommandInfo, ConcurrentDictionary<Guid, (Func<ICommandContext, CommandMatch, bool>,
            Action<IMessage, KeyValuePair<CommandMatch, ParseResult>, ICommandContext>)>> ByCommand =
            new ConcurrentDictionary<CommandInfo, ConcurrentDictionary<Guid, (Func<ICommandContext, CommandMatch, bool>,
                Action<IMessage, KeyValuePair<CommandMatch, ParseResult>, ICommandContext>)>>();

        public static CollectorController CollectCommand([NotNull] CommandInfo? info, Func<ICommandContext, CommandMatch, bool> predicate,
                                                         Action<CommandCollectorEventArgs> action) {
            info ??= default!;
            var collectorController = new CollectorController();
            var key = Guid.NewGuid();
            collectorController.Stop += (sender, args) => {
                if (!ByCommand.TryGetValue(info, out var value)) return;
                value.TryRemove(key, out _);
                if (value.IsEmpty) {
                    ByCommand.TryRemove(info, out _);
                }
            };
            var concurrentDictionary = ByCommand.GetOrAdd(info,
                arg =>
                    new ConcurrentDictionary<Guid, (Func<ICommandContext, CommandMatch, bool>,
                        Action<IMessage, KeyValuePair<CommandMatch, ParseResult>, ICommandContext>)>());
            concurrentDictionary.TryAdd(key,
                (predicate, (message, pair, arg3) => action(new CommandCollectorEventArgs(collectorController, message, pair, arg3))));
            return collectorController;
        }

        /// <summary>
        /// Method for CommandHandler, do not use!
        /// </summary>
        /// <returns>A value indicating whether to execute a command or not</returns>
        [Obsolete("Method for CommandHandler, do not invoke it manually")]
        public static bool OnCommandExecute(KeyValuePair<CommandMatch, ParseResult> info, ICommandContext context, IMessage message) {
            if (!ByCommand.TryGetValue(info.Key.Command, out var commandRequests)) return true;
            var keyValuePairs = commandRequests.ToList().Where(pair => pair.Value.Item1(context, info.Key)).ToList();
            foreach (var i in keyValuePairs) {
                logger.Swallow(() => i.Value.Item2(message, info, context));
            }

            return keyValuePairs.Count == 0;
        }

        #endregion

        private static Predicate<IMessage> ApplyFilters(Predicate<IMessage> initial, CollectorFilter filter) {
            return filter switch {
                CollectorFilter.Off        => initial,
                CollectorFilter.IgnoreSelf => (message => message.Author.Id != EnlivenBot.Client.CurrentUser.Id && initial(message)),
                CollectorFilter.IgnoreBots => (message => !message.Author.IsBot && !message.Author.IsWebhook && initial(message)),
                _                          => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
            };
        }

        private static Predicate<SocketReaction> ApplyFilters(Predicate<SocketReaction> initial, CollectorFilter filter) {
            return filter switch {
                CollectorFilter.Off        => initial,
                CollectorFilter.IgnoreSelf => (reaction => reaction.UserId != EnlivenBot.Client.CurrentUser.Id && initial(reaction)),
                CollectorFilter.IgnoreBots => reaction => {
                    try {
                        var user = reaction.User.GetValueOrDefault(EnlivenBot.Client.GetUser(reaction.UserId));
                        return !user.IsBot && !user.IsWebhook && initial(reaction);
                    }
                    catch (Exception) {
                        return false;
                    }
                },
                _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
            };
        }
    }
}