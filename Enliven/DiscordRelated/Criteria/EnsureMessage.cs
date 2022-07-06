using System;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Criteria;
using Discord;

namespace Bot.DiscordRelated.Criteria {
    public class EnsureMessage : INullableCriterion {
        private IMessageChannel _channel;
        private int _limit;
        private ulong _messageId;
        
        public EnsureMessage(IMessageChannel channel, IMessage message, int limit = 1) : this(channel, message.Id, limit) { }

        public EnsureMessage(IMessageChannel channel, ulong messageId, int limit = 1) {
            _limit = limit;
            _messageId = messageId;
            _channel = channel;
        }

        public bool IsNullableTrue { get; set; }

        public async Task<bool?> JudgeNullableAsync() {
            try {
                return await _channel.GetMessagesAsync(_limit).FlattenAsync()
                    .PipeAsync(messages => messages.Any(message => message.Id == _messageId));
            }
            catch (Exception) {
                return null;
            }
        }

        public static Task<bool> Exists(IMessageChannel channel, ulong messageId, int searchDepth = 1) {
            return (new EnsureMessage(channel, messageId, searchDepth) { IsNullableTrue = true } as ICriterion).JudgeAsync();
        }
        
        public static Task<bool> NotExists(IMessageChannel channel, ulong messageId, int searchDepth = 1) {
            return new EnsureMessage(channel, messageId, searchDepth) { IsNullableTrue = true }.Invert().JudgeAsync();
        }
    }
}