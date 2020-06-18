using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

#pragma warning disable 1998

namespace Bot.Utilities.Collector {
    public static class CollectorsUtils {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        static CollectorsUtils() {
            if (Program.CmdOptions.Observer) return;
            Program.Client.ReactionAdded += ClientOnReactionAdded;
            Program.Client.MessageReceived += ClientOnMessageReceived;
        }

        private static async Task ClientOnMessageReceived(SocketMessage arg) {
            new Thread(o => {
                if (MessageByChannel.TryGetValue(arg.Channel.Id, out var messageChannels))
                    ProcessMessage(arg, messageChannels.ToList());

                if (MessageByUser.TryGetValue(arg.Author.Id, out var messageUsers))
                    ProcessMessage(arg, messageUsers.ToList());
            }).Start();
        }

        private static void ProcessMessage(IMessage message, IEnumerable<KeyValuePair<Guid, (Predicate<IMessage>, Action<IMessage>)>> dictionary) {
            foreach (var i in dictionary.Where(pair => pair.Value.Item1(message))) {
                logger.Swallow(() => i.Value.Item2(message));
            }
        }


        private static async Task ClientOnReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3) {
            new Thread(o => {
                if (ReactionByChannel.TryGetValue(arg3.Channel.Id, out var reactionChannels))
                    ProcessReactions(arg3, reactionChannels.ToList());

                if (ReactionByEmote.TryGetValue(arg3.Emote, out var reactionEmotes))
                    ProcessReactions(arg3, reactionEmotes.ToList());

                if (ReactionByMessage.TryGetValue(arg3.MessageId, out var reactionMessages))
                    ProcessReactions(arg3, reactionMessages.ToList());

                if (ReactionByUser.TryGetValue(arg3.UserId, out var reactionUsers))
                    ProcessReactions(arg3, reactionUsers.ToList());
            }).Start();
        }

        private static void ProcessReactions(SocketReaction reaction,
                                             IEnumerable<KeyValuePair<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>> dictionary) {
            foreach (var i in dictionary.Where(pair => pair.Value.Item1(reaction))) {
                logger.Swallow(() => i.Value.Item2(reaction));
            }
        }

        #region Collect reaction by channel

        private static ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>> ReactionByChannel =
            new ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>>();

        public static CollectorController CollectReaction(IChannel channel, Predicate<SocketReaction> predicate,
                                                          Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            var collectorController = new CollectorController();
            var key = Guid.NewGuid();
            collectorController.Stop += (sender, args) => {
                if (!ReactionByChannel.TryGetValue(channel.Id, out var value)) return;
                value.TryRemove(key, out _);
                if (value.IsEmpty) {
                    ReactionByChannel.TryRemove(channel.Id, out _);
                }
            };
            var concurrentDictionary = ReactionByChannel.GetOrAdd(channel.Id,
                arg => new ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>());
            concurrentDictionary.TryAdd(key, (ApplyFilters(predicate, filter), reaction => action(new EmoteCollectorEventArgs(collectorController, reaction))));
            return collectorController;
        }

        #endregion

        #region Collect reaction by emote

        private static ConcurrentDictionary<IEmote, ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>> ReactionByEmote =
            new ConcurrentDictionary<IEmote, ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>>();

        public static CollectorController CollectReaction(IEmote emote, Predicate<SocketReaction> predicate,
                                                          Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            var collectorController = new CollectorController();
            var key = Guid.NewGuid();
            collectorController.Stop += (sender, args) => {
                if (!ReactionByEmote.TryGetValue(emote, out var value)) return;
                value.TryRemove(key, out _);
                if (value.IsEmpty) {
                    ReactionByEmote.TryRemove(emote, out _);
                }
            };
            var concurrentDictionary = ReactionByEmote.GetOrAdd(emote,
                arg => new ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>());
            concurrentDictionary.TryAdd(key, (ApplyFilters(predicate, filter), reaction => action(new EmoteCollectorEventArgs(collectorController, reaction))));
            return collectorController;
        }

        #endregion

        #region Collect reaction by message

        private static ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>> ReactionByMessage =
            new ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>>();

        public static CollectorController CollectReaction(IMessage message, Predicate<SocketReaction> predicate,
                                                          Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            var collectorController = new CollectorController();
            var key = Guid.NewGuid();
            collectorController.Stop += (sender, args) => {
                if (!ReactionByMessage.TryGetValue(message.Id, out var value)) return;
                value.TryRemove(key, out _);
                if (value.IsEmpty) {
                    ReactionByMessage.TryRemove(message.Id, out _);
                }
            };
            var concurrentDictionary = ReactionByMessage.GetOrAdd(message.Id,
                arg => new ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>());
            concurrentDictionary.TryAdd(key, (ApplyFilters(predicate, filter), reaction => action(new EmoteCollectorEventArgs(collectorController, reaction))));
            return collectorController;
        }

        #endregion

        #region Collect reaction by user

        private static ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>> ReactionByUser =
            new ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>>();

        public static CollectorController CollectReaction(IUser user, Predicate<SocketReaction> predicate, 
                                                          Action<EmoteCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            var collectorController = new CollectorController();
            var key = Guid.NewGuid();
            collectorController.Stop += (sender, args) => {
                if (!ReactionByUser.TryGetValue(user.Id, out var value)) return;
                value.TryRemove(key, out _);
                if (value.IsEmpty) {
                    ReactionByUser.TryRemove(user.Id, out _);
                }
            };
            var concurrentDictionary = ReactionByUser.GetOrAdd(user.Id,
                arg => new ConcurrentDictionary<Guid, (Predicate<SocketReaction>, Action<SocketReaction>)>());
            concurrentDictionary.TryAdd(key, (ApplyFilters(predicate, filter), reaction => action(new EmoteCollectorEventArgs(collectorController, reaction))));
            return collectorController;
        }

        #endregion

        #region Collect messages by user

        private static ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, (Predicate<IMessage>, Action<IMessage>)>> MessageByUser =
            new ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, (Predicate<IMessage>, Action<IMessage>)>>();

        public static CollectorController CollectMessage(IUser user, Predicate<IMessage> predicate, 
                                                         Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            var collectorController = new CollectorController();
            var key = Guid.NewGuid();
            collectorController.Stop += (sender, args) => {
                if (!MessageByUser.TryGetValue(user.Id, out var value)) return;
                value.TryRemove(key, out _);
                if (value.IsEmpty) {
                    MessageByUser.TryRemove(user.Id, out _);
                }
            };
            var concurrentDictionary = MessageByUser.GetOrAdd(user.Id,
                arg => new ConcurrentDictionary<Guid, (Predicate<IMessage>, Action<IMessage>)>());
            concurrentDictionary.TryAdd(key, (ApplyFilters(predicate, filter), reaction => action(new MessageCollectorEventArgs(collectorController, reaction))));
            return collectorController;
        }

        #endregion

        #region Collect messages by channel

        private static ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, (Predicate<IMessage>, Action<IMessage>)>> MessageByChannel =
            new ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, (Predicate<IMessage>, Action<IMessage>)>>();

        public static CollectorController CollectMessage(IChannel channel, Predicate<IMessage> predicate, 
                                                         Action<MessageCollectorEventArgs> action, CollectorFilter filter = CollectorFilter.Off) {
            var collectorController = new CollectorController();
            var key = Guid.NewGuid();
            collectorController.Stop += (sender, args) => {
                if (!MessageByChannel.TryGetValue(channel.Id, out var value)) return;
                value.TryRemove(key, out _);
                if (value.IsEmpty) {
                    MessageByChannel.TryRemove(channel.Id, out _);
                }
            };
            var concurrentDictionary = MessageByChannel.GetOrAdd(channel.Id,
                arg => new ConcurrentDictionary<Guid, (Predicate<IMessage>, Action<IMessage>)>());
            concurrentDictionary.TryAdd(key, (ApplyFilters(predicate, filter), reaction => action(new MessageCollectorEventArgs(collectorController, reaction))));
            return collectorController;
        }

        #endregion

        private static Predicate<IMessage> ApplyFilters(Predicate<IMessage> initial, CollectorFilter filter) {
            return filter switch {
                CollectorFilter.Off        => initial,
                CollectorFilter.IgnoreSelf => (message => message.Author.Id != Program.Client.CurrentUser.Id && initial(message)),
                CollectorFilter.IgnoreBots => (message => !message.Author.IsBot && !message.Author.IsWebhook && initial(message)),
                _                          => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
            };
        }
        
        private static Predicate<SocketReaction> ApplyFilters(Predicate<SocketReaction> initial, CollectorFilter filter) {
            return filter switch {
                CollectorFilter.Off        => initial,
                CollectorFilter.IgnoreSelf => (reaction => reaction.UserId != Program.Client.CurrentUser.Id && initial(reaction)),
                CollectorFilter.IgnoreBots => (reaction => !reaction.User.Value.IsBot && !reaction.User.Value.IsWebhook && initial(reaction)),
                _                          => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
            };
        }
    }
}