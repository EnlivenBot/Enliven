using System;
using System.Collections.Generic;
using System.Linq;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Config.Localization.Providers;
using Bot.Utilities;
using Discord;
using Discord.WebSocket;
using LiteDB;

namespace Bot {
    public class MessageHistory {
        public class MessageSnapshot {
            public DateTimeOffset EditTimestamp { get; set; }
            public string Value { get; set; }
        }

        [BsonId] public string Id => $"{ChannelId}:{MessageId}";

        [BsonField("A")] public ulong AuthorId { get; set; }

        [BsonField("C")] public ulong ChannelId { get; set; }

        [BsonField("M")] public ulong MessageId { get; set; }

        [BsonField("E")] public List<MessageSnapshot> Edits { get; set; } = new List<MessageSnapshot>();

        [BsonIgnore] public bool HistoryExists => Edits.Count != 0;
        
        [BsonIgnore] public bool IsIgnored  {
            get {
                var author = GetAuthor();
                if (author?.IsBot == true || author?.IsWebhook == true) {
                    return true;
                }

                return IgnoredMessages.IsIgnored(ChannelId.ToString(), MessageId.ToString());
            }
        }

        public void Save() {
            GlobalDB.Messages.Upsert(this);
        }

        public static MessageHistory Get(ulong channelId, ulong messageId, ulong authorId = default) {
            return GlobalDB.Messages.FindById($"{channelId}:{messageId}")?? new MessageHistory {
                AuthorId = authorId, MessageId = messageId, ChannelId = channelId
            };
        }

        public static MessageHistory Get(IMessage message) {
            return Get(message.Channel.Id, message.Id);
        }

        public void AddSnapshot(IMessage message) {
            AddSnapshot(message.EditedTimestamp ?? message.Timestamp, message.Content);
        }

        public void AddSnapshot(DateTimeOffset editTime, string newContent) {
            Edits.Add(new MessageSnapshot {
                EditTimestamp = editTime,
                Value = DiffMatchPatch.DiffMatchPatch.patch_toText(MessageHistoryManager.DiffMatchPatch.patch_make(GetLastContent(), newContent))
            });
        }

        public bool CanFitToEmbed(ILocalizationProvider loc, bool includeLastContent) {
            if (Edits.Count > 20)
                return false;
            var commonCount = 0;
            MessageSnapshot lastSnapshot = null;
            foreach (var edit in GetSnapshots(loc)) {
                if (edit.Value.Length > 1020)
                    return false;
                commonCount += edit.Value.Length + edit.EditTimestamp.ToString().Length;
                if (commonCount > 5500)
                    return false;
                lastSnapshot = edit;
            }

            if (!includeLastContent) return true;
            return commonCount + lastSnapshot.Value.Length + loc.Get("MessageHistory.LastContent").Format(lastSnapshot.EditTimestamp).Length <= 5500;
        }

        public SocketGuildUser GetAuthor() {
            return Program.Client.GetUser(AuthorId) as SocketGuildUser;
        }

        public EmbedBuilder GetEmbed(ILocalizationProvider loc) {
            var author = GetAuthor();
            var lastContent = GetLastContent();
            var eb = new EmbedBuilder();
            eb.AddField(loc.Get("MessageHistory.LastContent"),
                   $@">>> {lastContent.SafeSubstring(1000, "...")}")
              .AddField(loc.Get("MessageHistory.Author"), $"{author?.Username} (<@{AuthorId}>)", true)
              .AddField(loc.Get("MessageHistory.Channel"), $"<#{ChannelId}>", true)
              .WithFooter(loc.Get("MessageHistory.MessageId").Format(MessageId))
              .WithCurrentTimestamp();
            return eb;
        }

        public string GetLastContent() {
            return MessageHistoryManager.DiffMatchPatch.patch_apply(
                Edits.SelectMany(s1 => DiffMatchPatch.DiffMatchPatch.patch_fromText(s1.Value)).ToList(), "")[0].ToString();
        }

        public IEnumerable<MessageSnapshot> GetSnapshots(ILocalizationProvider loc) {
            var snapshots = new List<MessageSnapshot>();
            foreach (var edit in Edits) {
                var snapshot = MessageHistoryManager.DiffMatchPatch.patch_apply(DiffMatchPatch.DiffMatchPatch.patch_fromText(edit.Value),
                    snapshots.Count == 0 ? "" : snapshots.Last().Value)[0].ToString();
                if (snapshot == "###Unavailable$$$") {
                    snapshot = loc.Get("MessageHistory.PreviousUnavailable");
                }

                snapshots.Add(new MessageSnapshot {EditTimestamp = edit.EditTimestamp, Value = snapshot});
                yield return snapshots.Last();
            }
        }

        public IEnumerable<EmbedFieldBuilder> GetEditsAsFields(ILocalizationProvider loc, bool includeLastContent) {
            var embedFields = GetSnapshots(loc)
                             .Select(messageSnapshot => new EmbedFieldBuilder
                                  {Name = messageSnapshot.EditTimestamp.ToString(), Value = ">>> " + messageSnapshot.Value}).ToList();

            if (includeLastContent) {
                var lastContent = embedFields.Last();
                var embedFieldBuilder = new EmbedFieldBuilder {
                    Name = loc.Get("MessageHistory.LastContent").Format(lastContent.Name), Value = lastContent.Value
                };
                embedFields.Insert(0, embedFieldBuilder);
            }

            return embedFields;
        }
    }
}