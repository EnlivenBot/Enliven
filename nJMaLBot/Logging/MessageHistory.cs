using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization.Providers;
using Bot.Logging.Rendering;
using Bot.Utilities;
using DiffMatchPatch;
using Discord;
using Discord.WebSocket;
using DiscordChatExporter.Domain.Discord.Models;
using DiscordChatExporter.Domain.Discord.Models.Common;
using LiteDB;
using Attachment = DiscordChatExporter.Domain.Discord.Models.Attachment;
using Embed = DiscordChatExporter.Domain.Discord.Models.Embed;
using MessageType = DiscordChatExporter.Domain.Discord.Models.MessageType;

namespace Bot.Logging {
    public class MessageHistory {
        public class MessageSnapshot {
            public DateTimeOffset EditTimestamp { get; set; }
            public string Value { get; set; } = null!;
        }

        [BsonField("At")] public List<string>? Attachments { get; set; }

        [BsonField("U")] public bool IsHistoryUnavailable { get; set; }

        [BsonId] public string Id => $"{ChannelId}:{MessageId}";

        [BsonField("A")] public ulong AuthorId { get; set; }

        [BsonField("C")] public ulong ChannelId { get; set; }

        [BsonField("M")] public ulong MessageId { get; set; }

        [BsonField("E")] public List<MessageSnapshot> Edits { get; set; } = new List<MessageSnapshot>();

        [BsonIgnore] public bool HistoryExists => Edits.Count != 0;
        
        [BsonIgnore] private static Regex AttachmentRegex = new Regex(@"(\d+)\/(\d+)\/(.+?)$");

        public void Save() {
            GlobalDB.Messages.Upsert(this);
        }

        public static MessageHistory Get(ulong channelId, ulong messageId, ulong authorId = default) {
            return GlobalDB.Messages.FindById($"{channelId}:{messageId}") ?? new MessageHistory {
                AuthorId = authorId, MessageId = messageId, ChannelId = channelId
            };
        }

        public static MessageHistory Get(IMessage message) {
            return Get(message.Channel.Id, message.Id, message.Author.Id);
        }

        public void AddSnapshot(IMessage message) {
            AddSnapshot(message.EditedTimestamp ?? message.Timestamp, message.Content);
        }

        public void AddSnapshot(DateTimeOffset editTime, string newContent) {
            newContent ??= "";
            Edits.Add(new MessageSnapshot {
                EditTimestamp = editTime,
                Value = DiffMatchPatch.DiffMatchPatch.patch_toText(MessageHistoryManager.DiffMatchPatch.patch_make(GetLastContent(), newContent))
            });
        }

        public bool CanFitToEmbed(ILocalizationProvider loc) {
            if (Edits.Count > 20)
                return false;
            var commonCount = Attachments != null && Attachments.Count != 0
                ? loc.Get("MessageHistory.Attachments").Length + Attachments.Select(s => s.Length + 6).Sum()
                : 0;
            MessageSnapshot? lastSnapshot = null;
            foreach (var edit in GetSnapshots(loc)) {
                if (edit.Value.Length > 1020)
                    return false;
                commonCount += edit.Value.Length + edit.EditTimestamp.ToString().Length;
                if (commonCount > 5500)
                    return false;
                lastSnapshot = edit;
            }

            return commonCount - (lastSnapshot?.EditTimestamp.ToString()?.Length ?? 0) +
                loc.Get("MessageHistory.LastContent").Format(lastSnapshot?.EditTimestamp).Length <= 5500;
        }

        public SocketGuildUser? GetAuthor() {
            try {
                return Program.Client.GetUser(AuthorId) as SocketGuildUser;
            }
            catch (Exception) {
                return null;
            }
        }

        public string GetLastContent() {
            try {
                return MessageHistoryManager.DiffMatchPatch.patch_apply(
                    Edits.SelectMany(s1 => DiffMatchPatch.DiffMatchPatch.patch_fromText(s1.Value)).ToList(), "")[0].ToString() ?? "";
            }
            catch (Exception) {
                return "";
            }
        }

        public IEnumerable<MessageSnapshot> GetSnapshots(ILocalizationProvider loc, bool injectDiffsHighlight = false) {
            var snapshots = new List<MessageSnapshot>();
            if (IsHistoryUnavailable) {
                yield return new MessageSnapshot {Value = loc.Get("MessageHistory.PreviousUnavailable"), EditTimestamp = default};
            }

            foreach (var edit in Edits) {
                var last = snapshots.Count == 0 ? "" : snapshots.Last().Value;
                var snapshot = MessageHistoryManager.DiffMatchPatch.patch_apply(DiffMatchPatch.DiffMatchPatch.patch_fromText(edit.Value), last)[0].ToString() ??
                               "";

                snapshots.Add(new MessageSnapshot {EditTimestamp = edit.EditTimestamp, Value = snapshot});
                if (injectDiffsHighlight && snapshots.Count > 1) {
                    var diffMain = MessageHistoryManager.DiffMatchPatch.diff_main(last, snapshot);
                    MessageHistoryManager.DiffMatchPatch.diff_cleanupSemantic(diffMain);
                    yield return new MessageSnapshot {EditTimestamp = edit.EditTimestamp, Value = DiffsToHtmlEncode(diffMain)};
                }
                else {
                    yield return snapshots.Last();
                }
            }
        }

        public IEnumerable<EmbedFieldBuilder> GetEditsAsFields(ILocalizationProvider loc) {
            var embedFields = GetSnapshots(loc)
                             .Select(messageSnapshot => new EmbedFieldBuilder
                                  {Name = messageSnapshot.EditTimestamp.ToString(), Value = ">>> " + messageSnapshot.Value}).ToList();

            var lastContent = embedFields.Last();
            lastContent.Name = loc.Get("MessageHistory.LastContent").Format(lastContent.Name);

            return embedFields;
        }

        public async Task<string> ExportToHtml(ILocalizationProvider loc, bool injectDiffsHighlight = true) {
            // Create context
            var context = LogExportContext.Create(ChannelId, out var members);
            var stream = new MemoryStream();

            // Render messages
            var renderer = new LogMessageWriter(stream, context, "Dark");
            try {
                var user = ConstructUser(AuthorId);
                var member = Member.CreateForUser(user);
                members.Add(member);

                await renderer.WritePreambleAsync();
                if (Attachments != null && Attachments.Count != 0) {
                    await renderer.WriteMessageAsync(new Message("", MessageType.Default, user, DateTimeOffset.MinValue, null, true, "",
                        Attachments.Select(s => {
                            var match = AttachmentRegex.Match(s);
                            var attachment = new Attachment(match.Groups[2].Value, s, match.Groups[3].Value, null, null, FileSize.FromBytes(0));
                            return attachment;
                        }).ToImmutableList(), new List<Embed>(), new List<Reaction>(), new List<User>()));
                }
                foreach (var messageSnapshot in GetSnapshots(loc, injectDiffsHighlight)) {
                    foreach (var userMention in GetUserMentions(messageSnapshot.Value)) members.Add(Member.CreateForUser(userMention));

                    await renderer.WriteMessageAsync(new Message(MessageId.ToString(), MessageType.Default, user, messageSnapshot.EditTimestamp,
                        null, false, messageSnapshot.Value,
                        new List<Attachment>(), new List<Embed>(),
                        new List<Reaction>(), members.Select(member1 => member1.User).ToList()));
                }

                await renderer.WritePostambleAsync();
                try {
                    stream.Position = 0;
                    var readToEndAsync = await new StreamReader(stream).ReadToEndAsync();
                    return HtmlDiffsUnEncode(readToEndAsync);
                }
                finally {
                    await renderer.DisposeAsync();
                }
            }
            finally {
                await renderer.DisposeAsync();
            }
        }

        private static IEnumerable<User> GetUserMentions(string text) {
            foreach (Match? m in Regex.Matches(text, @"(?<=<@|<@!)[0-9]{18}(?=>)", RegexOptions.Multiline))
                if (m != null)
                    yield return ConstructUser(Convert.ToUInt64(m.Value));
        }

        private static User ConstructUser(ulong userId) {
            return ConstructUser(Program.Client.GetUser(userId));
        }

        private static User ConstructUser(SocketUser user) {
            return new User(user.Id.ToString(), user.IsBot, user.DiscriminatorValue, user.Username, user.AvatarId);
        }

        private static string DiffsToHtmlEncode(List<Diff> diffs) {
            var html = new StringBuilder();
            foreach (var aDiff in diffs) {
                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                switch (aDiff.Operation) {
                    case Operation.Insert:
                        html.Append("࢔")
                            .Append(aDiff.Text)
                            .Append("࢕");
                        break;
                    case Operation.Delete:
                        html.Append("࢖")
                            .Append(aDiff.Text)
                            .Append("ࢗ");
                        break;
                    case Operation.Equal:
                        html.Append(aDiff.Text);
                        break;
                }
            }

            return html.ToString();
        }

        private static string HtmlDiffsUnEncode(string encoded) {
            return encoded.Replace("࢔", "<span style=\"background:DarkGreen;\">").Replace("࢕", "</span>")
                          .Replace("࢖", "<span class=\"removed\">").Replace("ࢗ", "</span>");
        }

        public async Task<IUserMessage?> GetRealMessage() {
            try {
                var textChannel = (ITextChannel) Program.Client.GetChannel(ChannelId);
                var messageAsync = await textChannel?.GetMessageAsync(MessageId)!;
                return messageAsync as IUserMessage;
            }
            catch (Exception) {
                return null;
            }
        }
    }
}