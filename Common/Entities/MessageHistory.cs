using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common.Localization.Providers;
using DiffMatchPatch;
using Discord;
using LiteDB;

namespace Common.Entities {
    public class MessageHistory {
        [BsonField("At")] public List<string>? Attachments { get; set; }

        [BsonField("U")] public bool IsHistoryUnavailable { get; set; }

        [BsonId] public string Id { get; internal set; } = null!;

        [BsonField("A")] public ulong AuthorId { get; set; }

        [BsonIgnore] public ulong ChannelId => Convert.ToUInt64(Id.Split(":")[0]);

        [BsonIgnore] public ulong MessageId => Convert.ToUInt64(Id.Split(":")[1]);

        [BsonField("E")] internal List<MessageSnapshotEntity> Edits { get; set; } = new List<MessageSnapshotEntity>();

        [BsonIgnore] public bool HistoryExists => Edits.Count != 0;

        [BsonIgnore] public bool HasAttachments => Attachments != null && Attachments.Count != 0;

        [BsonIgnore] public ISubject<MessageHistory> SaveRequest = new Subject<MessageHistory>();

        public void Save() {
            SaveRequest.OnNext(this);
        }

        public void AddSnapshot(IMessage message) {
            AddSnapshot(message.EditedTimestamp ?? message.Timestamp, message.Content);
        }

        public void AddSnapshot(DateTimeOffset editTime, string? newContent) {
            newContent ??= "";
            Edits.Add(new MessageSnapshotEntity {
                EditTimestamp = editTime,
                DiffString = Patch.Compute(GetLastContent(), newContent).ToText()
            });
        }

        public string GetLastContent() {
            try {
                return Edits.SelectMany(s1 => PatchList.Parse(s1.DiffString)).Apply("").newText;
            }
            catch (Exception) {
                return "";
            }
        }

        public IEnumerable<MessageSnapshot> GetSnapshots(ILocalizationProvider loc) {
            var snapshots = new List<MessageSnapshot>();
            if (IsHistoryUnavailable) {
                yield return MessageSnapshot.WithMessageUnavailable(this, loc);
            }

            foreach (var edit in Edits) {
                var lastContent = snapshots.Count == 0 ? "" : snapshots.Last().CurrentContent;
                var currentContent = PatchList.Parse(edit.DiffString).Apply(lastContent).newText;

                snapshots.Add(new MessageSnapshot(this, edit.EditTimestamp, snapshots.Count, currentContent, lastContent));
                yield return snapshots.Last();
            }
        }
        
        public async Task<IMessage?> GetRealMessage(EnlivenShardedClient client) {
            try {
                var textChannel = (ITextChannel) client.GetChannel(ChannelId);
                return await textChannel.GetMessageAsync(MessageId)!;
            }
            catch (Exception) {
                return null;
            }
        }
    }
}