using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace Bot.DiscordRelated.Criteria {
    public class EnsureLastMessage : INullableCriterion {
        private IMessageChannel _channel;
        private int _limit;
        private ulong _messageId;
        
        public EnsureLastMessage(IMessageChannel channel, IMessage message, int limit = 1) : this(channel, message.Id, limit) { }

        public EnsureLastMessage(IMessageChannel channel, ulong messageId, int limit = 1) {
            _limit = limit;
            _messageId = messageId;
            _channel = channel;
        }

        public bool IsNullableTrue { get; set; }

        public async Task<bool?> JudgeNullableAsync() {
            try {
                return (await _channel.GetMessagesAsync(_limit).FlattenAsync()).FirstOrDefault(message => message.Id == _messageId) != null;
            }
            catch (Exception) {
                return null;
            }
        }
    }
}