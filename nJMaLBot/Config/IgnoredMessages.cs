using Discord;
using LiteDB;

namespace Bot.Config {
    public static class IgnoredMessages {
        private static readonly ILiteCollection<ListedEntry> ignoredMessages = GlobalDB.Database.GetCollection<ListedEntry>(@"IgnoredMessages");
        public static void AddMessageToIgnore(IMessage arg) {
            AddMessageToIgnore(arg.Channel.Id.ToString(), arg.Id.ToString());
        }

        public static void AddMessageToIgnore(string channelId, string messageId) {
            var channelIgnoredMessages = ignoredMessages.FindById(channelId) ?? new ListedEntry {Id = channelId};
            channelIgnoredMessages.Data.Add(messageId);
            ignoredMessages.Upsert(channelIgnoredMessages);
        }

        public static bool IsIgnored(string channelId, string messageId) {
            var listedEntry = ignoredMessages.FindById(channelId);
            return listedEntry != null && listedEntry.Data.Contains(messageId);
        }

        public static void RemoveIgnore(string channelId, string messageId) {
            var listedEntry = ignoredMessages.FindById(channelId);
            if (listedEntry == null)
                return;
            
            listedEntry.Data.Remove(messageId);
            
            if (listedEntry.Data.Count == 0) {
                ignoredMessages.Delete(channelId);
            }
            else {
                ignoredMessages.Upsert(listedEntry);
            }
        }
    }
}