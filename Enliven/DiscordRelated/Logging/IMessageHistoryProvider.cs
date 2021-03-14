using System;
using Discord;

namespace Bot.DiscordRelated.Logging {
    public interface IMessageHistoryProvider {
        MessageHistory Get(ulong channelId, ulong messageId, ulong authorId = default);
        MessageHistory Get(IMessage message);
        MessageHistory FromMessage(IMessage arg);
        void Delete(MessageHistory messageHistory);
        void Delete(ulong channelId, ulong messageId);
        void Delete(string id);
        int DeleteMany(Func<MessageHistory, bool> func);
    }
}