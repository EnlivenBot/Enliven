using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Discord;
using LiteDB;

namespace Bot.DiscordRelated.Logging {
    public class MessageHistoryProvider : IMessageHistoryProvider {
        #pragma warning disable 618
        private ILiteCollection<MessageHistory> _liteCollection;

        public MessageHistoryProvider(ILiteCollection<MessageHistory> liteCollection) {
            _liteCollection = liteCollection;
        }

        private ConcurrentDictionary<string, MessageHistory> _cache = new ConcurrentDictionary<string, MessageHistory>();

        public MessageHistory Get(ulong channelId, ulong messageId, ulong authorId = default) {
            string id = $"{channelId}:{messageId}";
            return _cache.GetOrAdd(id, s => {
                var history = _liteCollection.FindById(id) ?? new MessageHistory {AuthorId = authorId, Id = id};
                history.SaveRequest.Subscribe(messageHistory => _liteCollection.Upsert(messageHistory));
                return history;
            });
        }

        public MessageHistory Get(IMessage message) {
            return Get(message.Channel.Id, message.Id, message.Author.Id);
        }

        public MessageHistory FromMessage(IMessage arg) {
            var history = new MessageHistory {
                AuthorId = arg.Author.Id,
                Id = $"{arg.Channel.Id}:{arg.Id}",
                Edits = new List<MessageHistory.MessageSnapshot> {
                    new MessageHistory.MessageSnapshot {
                        EditTimestamp = arg.CreatedAt,
                        Value = Utilities.DiffMatchPatch.DiffMatchPatch.patch_toText(MessageHistoryService.DiffMatchPatch.patch_make("", arg.Content))
                    }
                },
                Attachments = arg.Attachments.Select(attachment => attachment.Url).ToList()
            };
            history.SaveRequest.Subscribe(messageHistory => _liteCollection.Upsert(messageHistory));
            return history;
        }

        public void Delete(MessageHistory messageHistory) {
            Delete(messageHistory.Id);
        }

        public void Delete(ulong channelId, ulong messageId) {
            Delete($"{channelId}:{messageId}");
        }

        public void Delete(string id) {
            _liteCollection.Delete(id);
        }

        public int DeleteMany(Func<MessageHistory, bool> func) {
            return _liteCollection.DeleteMany(history => func(history));
        }
        #pragma warning restore 618
    }
}