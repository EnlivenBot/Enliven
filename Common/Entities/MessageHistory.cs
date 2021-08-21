using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common.Config;
using Common.Localization.Providers;
using DiffMatchPatch;
using Discord;
using LiteDB;

namespace Common.Entities {
    public class MessageHistory {
        public List<string>? Attachments { get; set; }

        public bool IsHistoryUnavailable { get; set; }

        [BsonId] public string Id { get; internal set; } = null!;

        public UserLink Author { get; set; } = null!;

        internal List<MessageSnapshotEntity> Edits { get; set; } = new();
        
        [BsonIgnore] public ulong ChannelId => Convert.ToUInt64(Id.Split(":")[0]);

        [BsonIgnore] public ulong MessageId => Convert.ToUInt64(Id.Split(":")[1]);
        
        [BsonIgnore] public bool HistoryExists => Edits.Count != 0;

        [BsonIgnore] public bool HasAttachments => Attachments != null && Attachments.Count != 0;

        [BsonIgnore] public ISubject<MessageHistory> SaveRequest = new Subject<MessageHistory>();

        [BsonIgnore] public int EditsCount => Edits.Count;

        public void Save() {
            SaveRequest.OnNext(this);
        }

        public void AddSnapshot(IMessage message) {
            AddSnapshot(message.EditedTimestamp ?? message.Timestamp, message.Content);
        }

        public void AddSnapshot(DateTimeOffset editTime, string? newContent) {
            newContent ??= "";
            #pragma warning disable 618
            AddSnapshotInternal(editTime, Patch.Compute(GetLastContent(), newContent).ToText());
            #pragma warning restore 618
        }
        
        [Obsolete("Use AddSnapshot. This method for ")]
        internal void AddSnapshotInternal(DateTimeOffset editTime, string diff) {
            Edits.Add(new MessageSnapshotEntity {
                EditTimestamp = editTime,
                DiffString = diff
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
                if (await client.GetChannelAsync(ChannelId) is not ITextChannel textChannel) return null;
                return await textChannel.GetMessageAsync(MessageId);
            }
            catch (Exception) {
                return null;
            }
        }
    }
}