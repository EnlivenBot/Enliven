using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Common.Config;
using Discord;
using LiteDB;

namespace Common.Entities {
    public class MessageHistoryProvider {
        #pragma warning disable 618
        private ILiteCollection<MessageHistory> _liteCollection;

        public MessageHistoryProvider(ILiteCollection<MessageHistory> liteCollection) {
            _liteCollection = liteCollection;
        }

        private ConcurrentDictionary<string, MessageHistory> _cache = new ConcurrentDictionary<string, MessageHistory>();

        public MessageHistory? Get(ulong channelId, ulong messageId) {
            string id = $"{channelId}:{messageId}";
            if (_cache.TryGetValue(id, out var cachedResult)) {
                return cachedResult;
            }

            var history = _cache[id] = _liteCollection.FindById(id);
            history?.SaveRequest.Subscribe(messageHistory => _liteCollection.Upsert(messageHistory));
            return history;
        }

        public MessageHistory? Get(IMessage message) {
            return Get(message.Channel.Id, message.Id);
        }

        public MessageHistory GetAndSync(IMessage message) {
            var history = Get(message);
            if (history == null) {
                history = new MessageHistory {
                    Author = new UserLink(message.Author.Id),
                    Id = $"{message.Channel.Id}:{message.Id}",
                    Attachments = message.Attachments.Select(attachment => attachment.Url).ToList(),
                    IsHistoryUnavailable = message.EditedTimestamp != null
                };
                history.SaveRequest.Subscribe(messageHistory => _liteCollection.Upsert(messageHistory));
                _cache[history.Id] = history;
            }

            if (history.Edits.All(entity => entity.EditTimestamp != message.EditedTimestamp)) {
                history.AddSnapshot(message);
                history.Save();
            }
            
            return history;
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