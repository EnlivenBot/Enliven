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

        public EmbedBuilder GetEmbed() {
            var author = GetAuthor();
            var eb = new EmbedBuilder();
            eb.AddField("Последнее содержимое:",
                   $@">>> {MessageHistoryManager.SafeContentCut(Edits.Aggregate("", (s, snapshot) =>
                       MessageHistoryManager.DiffMatchPatch.patch_apply(
                           MessageHistoryManager.DiffMatchPatch.patch_fromText(snapshot.Patch), s)[0].ToString()), 1000)}")
              .AddField("Автор", $"{author?.Username} (<@{AuthorId}>)", true)
              .AddField("Канал", $"<#{ChannelId}>", true)
              .WithFooter($"Message ID: {MessageId}")
              .WithCurrentTimestamp();
            return eb;
        }

        public class MessageSnapshot {
            public DateTimeOffset EditTimestamp { get; set; }
            public string Patch { get; set; }
        }
    }

    public static class MessageHistoryManager {
        public static diff_match_patch DiffMatchPatch = new diff_match_patch();

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

        public static async Task<byte[]> RenderLog(MessageHistory messageHistory) {
            await ExportHelper.ExportHistoryAsync(messageHistory, $"{messageHistory.Id}.html");
            var converter = new HtmlConverter();
            var bytes = converter.FromHtmlString(File.ReadAllText($"{messageHistory.Id}.html"), 512);
            File.Delete($"{messageHistory.Id}.html");
            return bytes;
        }

        public static async Task<IUserMessage> GetRealMessage(ulong channelId, ulong messageId) {
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
            var ourPermissions = channel.GetUser(Program.Client.CurrentUser.Id).GetPermissions(channel);
            if (!user.GetPermissions(channel).SendMessages || !ourPermissions.SendMessages || !ourPermissions.AttachFiles)
                textChannel = await user.GetOrCreateDMChannelAsync();

            var messageLog = MessageHistory.Get(channelId, messageId);
            var message = await GetRealMessage(channelId, messageId);
            if (messageLog != null) {
                var logImage = await RenderLog(messageLog);
                var embedBuilder = messageLog.GetEmbed()
                                             .WithTitle("История изменений сообщения")
                                             .WithDescription("История изменений отражена на изображении выше.")
                                             .WithUrl(message?.GetJumpUrl());
                (await textChannel.SendFileAsync(new MemoryStream(logImage),
                    $"History-{messageLog.ChannelId}-{messageLog.MessageId}.jpg",
                    "===========================================", false, embedBuilder.Build())).DelayedDelete(TimeSpan.FromMinutes(10));
            }
            else {
                var emberBuilder = new EmbedBuilder().WithTitle("История изменений сообщения").WithUrl(message?.GetJumpUrl());
                emberBuilder.WithDescription(
                    message == null
                        ? "Не удалось получить историю изменений: сообщение не найдено.\nВозможные причины:\n- Неправильный ID\n- Сообщение удалено\n- Бот больше не имеет к нему доступ."
                        : GlobalDB.IgnoredMessages.FindById($"{channelId}:{messageId}") != null || message.Author.IsBot || message.Author.IsWebhook
                            ? "Не удалось получить историю изменений, сообщение игнорируется.\nВозможные причины:\n- Сообщение написано ботом"
                            : "Не удалось получить историю изменений, история не найдена.\nВозможные причины:\n- Сообщение написано до появления бота на сервере\n- Бот во время написания сообщения был оффлайн\n- Сообщение написано в канале лога\n\nСообщение залоггированно.");
                if (message != null) {
                    emberBuilder.AddField("Автор", $"{message.Author?.Username} (<@{message.Author?.Id}>)", true);
                    if (!(message.Author.IsBot || message.Author.IsWebhook)) {
                        #pragma warning disable 4014
                        ClientOnMessageUpdated(null, message, Program.Client.GetChannel(channelId) as ISocketMessageChannel);
                        #pragma warning restore 4014
                    }
                }

                emberBuilder.AddField("Канал", $"<#{channelId}>", true)
                            .WithFooter($"Message ID: {messageId}")
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
            var message = MessageHistory.Get(arg2.Id, arg1.Id);

            if (guild.GetChannel(ChannelFunction.Log, out var logChannel))
                if (logChannel.Id != arg2.Id)
                    try {
                        var logImage = await RenderLog(message);
                        var embedBuilder = message.GetEmbed()
                                                  .WithTitle("Сообщение было удалено")
                                                  .WithDescription("История изменений отражена на изображении выше.");
                        await ((ISocketMessageChannel) logChannel).SendFileAsync(new MemoryStream(logImage),
                            $"History-{message.ChannelId}-{message.MessageId}.jpg",
                            "===========================================", false, embedBuilder.Build());
                    }
                    catch (Exception e) {
                        var color = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Exception during print log message!");
                        Console.WriteLine(e.ToString());
                        Console.ForegroundColor = color;
                    }

            if (message != null) GlobalDB.Messages.Delete(id);
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
                    Patch = DiffMatchPatch.patch_toText(DiffMatchPatch.patch_make("", Localization.Get(channel.Id, "OnUpdate.PreviousUnavailable")))
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
                EditTimestamp = after.EditedTimestamp ?? after.Timestamp,
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