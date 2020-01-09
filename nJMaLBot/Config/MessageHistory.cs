using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Utilities;
using CoreHtmlToImage;
using DiffMatchPatch;
using Discord;
using Discord.WebSocket;
using LiteDB;

namespace Bot {
    public class MessageHistory {
        [BsonId] public string Id => $"{ChannelId}:{MessageId}";

        [BsonField("A")] public ulong AuthorId { get; set; }

        [BsonField("C")] public ulong ChannelId { get; set; }

        [BsonField("M")] public ulong MessageId { get; set; }

        [BsonField("E")] public List<MessageSnapshot> Edits { get; set; } = new List<MessageSnapshot>();

        public void Save() {
            GlobalDB.Messages.Upsert(this);
        }

        public static MessageHistory Get(ulong channelId, ulong messageId) {
            return GlobalDB.Messages.FindById($"{channelId}:{messageId}");
        }

        public SocketGuildUser GetAuthor() {
            return Program.Client.GetUser(AuthorId) as SocketGuildUser;
        }

        public EmbedBuilder GetEmbed(LocalizationProvider loc) {
            var author = GetAuthor();
            var eb = new EmbedBuilder();
            eb.AddField(loc.Get("MessageHistory.LastContent"),
                   $@">>> {MessageHistoryManager.SafeContentCut(Edits.Aggregate("", (s, snapshot) =>
                       MessageHistoryManager.DiffMatchPatch.patch_apply(
                           MessageHistoryManager.DiffMatchPatch.patch_fromText(snapshot.Patch), s)[0].ToString()), 1000)}")
              .AddField(loc.Get("MessageHistory.Author"), $"{author?.Username} (<@{AuthorId}>)", true)
              .AddField(loc.Get("MessageHistory.Channel"), $"<#{ChannelId}>", true)
              .WithFooter(loc.Get("MessageHistory.MessageId").Format(MessageId))
              .WithCurrentTimestamp();
            return eb;
        }

        public class MessageSnapshot {
            public DateTimeOffset EditTimestamp { get; set; }
            public string Patch { get; set; }
        }
    }

    public static class MessageHistoryManager {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static readonly diff_match_patch DiffMatchPatch = new diff_match_patch();

        static MessageHistoryManager() {
            Program.OnClientConnect += (sender, client) => SetHandlers(client);
        }

        private static void SetHandlers(DiscordSocketClient client) {
            client.MessageReceived += ClientOnMessageReceived;
            client.MessageUpdated += async (before, after, channel) => await ClientOnMessageUpdated(await before.GetOrDownloadAsync(), after, channel);
            client.MessageDeleted += ClientOnMessageDeleted;
        }

        public static string SafeContentCut(string content, int maxLength = 1024) {
            if (content.Length <= maxLength)
                return content;
            return content.Substring(0, maxLength - 3) + "...";
        }

        private static async Task<byte[]> RenderLog(MessageHistory messageHistory) {
            await ExportHelper.ExportHistoryAsync(messageHistory, $"{messageHistory.Id}.html");
            var converter = new HtmlConverter();
            var bytes = converter.FromHtmlString(File.ReadAllText($"{messageHistory.Id}.html"), 512);
            File.Delete($"{messageHistory.Id}.html");
            return bytes;
        }

        private static async Task<IUserMessage> GetRealMessage(ulong channelId, ulong messageId) {
            try {
                var textChannel = (ITextChannel) Program.Client.GetChannel(channelId);
                var messageAsync = await textChannel?.GetMessageAsync(messageId);
                return messageAsync as IUserMessage;
            }
            catch (Exception) {
                return null;
            }
        }

        public static async Task PrintLog(ulong messageId, ulong channelId, SocketTextChannel channel, IGuildUser user) {
            IMessageChannel textChannel = channel;
            var loc = new LocalizationProvider(channel.Guild.Id);
            var ourPermissions = channel.GetUser(Program.Client.CurrentUser.Id).GetPermissions(channel);
            if (!user.GetPermissions(channel).SendMessages || !ourPermissions.SendMessages || !ourPermissions.AttachFiles)
                textChannel = await user.GetOrCreateDMChannelAsync();

            var messageLog = MessageHistory.Get(channelId, messageId);
            var message = await GetRealMessage(channelId, messageId);
            if (messageLog != null) {
                var logImage = await RenderLog(messageLog);
                var embedBuilder = messageLog.GetEmbed(loc)
                                             .WithTitle(loc.Get("MessageHistory.LogTitle"))
                                             .AddField(loc.Get("MessageHistory.Requester"), $"{user.Username} (<@{user.Id}>)", true)
                                             .WithDescription(loc.Get("MessageHistory.ImageDescription").Format(message?.GetJumpUrl()));
                (await textChannel.SendFileAsync(new MemoryStream(logImage),
                    $"History-{messageLog.ChannelId}-{messageLog.MessageId}.jpg",
                    "===========================================", false, embedBuilder.Build())).DelayedDelete(TimeSpan.FromMinutes(10));
            }
            else {
                var emberBuilder = new EmbedBuilder().WithTitle(loc.Get("MessageHistory.LogTitle"));
                emberBuilder.WithDescription(
                    message == null
                        ? loc.Get("MessageHistory.MessageNull")
                        : GlobalDB.IgnoredMessages.FindById($"{channelId}:{messageId}") != null || message.Author.IsBot || message.Author.IsWebhook
                            ? loc.Get("MessageHistory.MessageIgnore").Format(message.GetJumpUrl())
                            : loc.Get("MessageHistory.MessageWithoutHistory").Format(message.GetJumpUrl()));
                if (message != null) {
                    emberBuilder.AddField(loc.Get("MessageHistory.Author"), $"{message.Author?.Username} (<@{message.Author?.Id}>)",
                        true);
                    if (!(message.Author?.IsBot == true || message.Author?.IsWebhook == true)) {
                        #pragma warning disable 4014
                        ClientOnMessageUpdated(null, message, Program.Client.GetChannel(channelId) as ISocketMessageChannel);
                        #pragma warning restore 4014
                    }
                }

                emberBuilder.AddField(loc.Get("MessageHistory.Channel"), $"<#{channelId}>", true)
                            .AddField(loc.Get("MessageHistory.Requester"), $"{user.Username} (<@{user.Id}>)", true)
                            .WithFooter(loc.Get("MessageHistory.MessageId").Format(messageId))
                            .WithCurrentTimestamp();
                (await channel.SendMessageAsync(null, false, emberBuilder.Build())).DelayedDelete(TimeSpan.FromMinutes(10));
            }
        }

        private static async Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> arg1, ISocketMessageChannel arg2) {
            if (!(arg2 is ITextChannel textChannel)) return;

            var id = $"{textChannel.Id}:{arg1.Id}";
            if (GlobalDB.IgnoredMessages.FindById(id) != null) {
                GlobalDB.IgnoredMessages.Delete(id);
                return;
            }

            var guild = GuildConfig.Get(textChannel.GuildId);
            var loc = new LocalizationProvider(arg2.Id);
            var messageLog = MessageHistory.Get(arg2.Id, arg1.Id);

            if (guild.GetChannel(ChannelFunction.Log, out var logChannel))
                if (logChannel.Id != arg2.Id)
                    try {
                        if (messageLog != null) {
                            var logImage = await RenderLog(messageLog);
                            var embedBuilder = messageLog.GetEmbed(loc)
                                                         .WithTitle(loc.Get("MessageHistory.MessageWasDeleted"))
                                                         .WithDescription(loc.Get("MessageHistory.ImageDescription"));
                            await ((ISocketMessageChannel) logChannel).SendFileAsync(new MemoryStream(logImage),
                                $"History-{messageLog.ChannelId}-{messageLog.MessageId}.jpg",
                                "===========================================", false, embedBuilder.Build());
                        }
                        else {
                            var embedBuilder = new EmbedBuilder()
                                              .WithTitle(loc.Get("MessageHistory.MessageWasDeleted"))
                                              .WithDescription(loc.Get("MessageHistory.OnDeleteWithoutHistory"));
                            var message = await arg1.GetOrDownloadAsync();
                            embedBuilder.AddField(loc.Get("MessageHistory.LastContent"),
                                message == null ? loc.Get("MessageHistory.Unavailable") : message.Content);
                            await ((ISocketMessageChannel) logChannel).SendMessageAsync("===========================================", false,
                                embedBuilder.Build());
                        }
                    }
                    catch (Exception e) {
                        logger.Error(e, "Failed to print log message");
                    }

            if (messageLog != null) GlobalDB.Messages.Delete(id);
        }

        private static async Task ClientOnMessageUpdated(IMessage before, IMessage after, ISocketMessageChannel channel) {
            if (!(after.Channel is ITextChannel textChannel)) return;
            var id = $"{textChannel.Id}:{after.Id}";
            if (after.Author.IsBot || after.Author.IsWebhook) {
                GlobalDB.IgnoredMessages.Upsert(id, new BsonDocument());
                return;
            }

            var messageHistory = MessageHistory.Get(textChannel.Id, after.Id) ??
                                 new MessageHistory {
                                     AuthorId = after.Author.Id,
                                     ChannelId = textChannel.Id, MessageId = after.Id
                                 };

            if (messageHistory.Edits.Count == 0) {
                messageHistory.Edits.Add(new MessageHistory.MessageSnapshot {
                    EditTimestamp = after.CreatedAt,
                    Patch = DiffMatchPatch.patch_toText(DiffMatchPatch.patch_make("", Localization.Get(textChannel.Guild.Id, "MessageHistory.PreviousUnavailable")))
                });

                if (before != null)
                    messageHistory.Edits.Add(new MessageHistory.MessageSnapshot {
                        EditTimestamp = before.EditedTimestamp ?? before.Timestamp,
                        Patch = DiffMatchPatch.patch_toText(DiffMatchPatch.patch_make(
                            DiffMatchPatch.patch_apply(messageHistory.Edits.SelectMany(s1 => DiffMatchPatch.patch_fromText(s1.Patch)).ToList(), "")[0]
                                          .ToString(), before.Content))
                    });
            }

            messageHistory.Edits.Add(new MessageHistory.MessageSnapshot {
                EditTimestamp = DateTimeOffset.Now,
                Patch = DiffMatchPatch.patch_toText(DiffMatchPatch.patch_make(
                    DiffMatchPatch.patch_apply(messageHistory.Edits.SelectMany(s1 => DiffMatchPatch.patch_fromText(s1.Patch)).ToList(), "")[0].ToString(),
                    after.Content))
            });

            messageHistory.Save();
        }

        private static async Task ClientOnMessageReceived(SocketMessage arg) {
            if (!(arg.Channel is ITextChannel textChannel)) return;
            var id = $"{textChannel.Id}:{arg.Id}";
            if (arg.Author.IsBot || arg.Author.IsWebhook) {
                GlobalDB.IgnoredMessages.Insert(id, new BsonDocument());
                return;
            }

            new MessageHistory {
                AuthorId = arg.Author.Id,
                ChannelId = textChannel.Id, MessageId = arg.Id,
                Edits = new List<MessageHistory.MessageSnapshot> {
                    new MessageHistory.MessageSnapshot
                        {EditTimestamp = arg.CreatedAt, Patch = DiffMatchPatch.patch_toText(DiffMatchPatch.patch_make("", arg.Content))}
                }
            }.Save();
        }
    }
}