using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot.Commands;
using Bot.Config;
using Bot.Config.Localization.Providers;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.Emoji;
using CoreHtmlToImage;
using DiffMatchPatch;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using LiteDB;

namespace Bot {
    public static class MessageHistoryManager {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static readonly DiffMatchPatch.DiffMatchPatch DiffMatchPatch = new DiffMatchPatch.DiffMatchPatch();

        public static void SetHandlers() {
            // Message created handled located in CommandHandler
            Program.Client.MessageUpdated += ClientOnMessageUpdated;
            Program.Client.MessageDeleted += ClientOnMessageDeleted;
            Program.Client.ChannelDestroyed += ClientOnChannelDestroyed;
            Program.Client.LeftGuild += ClientOnLeftGuild;
            CollectorsUtils.CollectReaction(CommonEmoji.LegacyBook, reaction => true, async eventArgs => {
                await eventArgs.RemoveReason();
                try {
                    await PrintLog(MessageHistory.Get(eventArgs.Reaction.Channel.Id, eventArgs.Reaction.MessageId),
                        (SocketTextChannel) eventArgs.Reaction.Channel,
                        new GuildLocalizationProvider(((SocketTextChannel) eventArgs.Reaction.Channel).Guild.Id),
                        (IGuildUser) eventArgs.Reaction.User.Value);
                }
                catch (Exception e) {
                    logger.Error(e, "Faled to print log");
                    throw;
                }
            }, CollectorFilter.IgnoreBots);
        }

        private static Task ClientOnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3) {
            Task.Run(() => {
                var history = MessageHistory.Get(arg2);
                if (history.Edits.Count == 0) {
                    history.AddSnapshot(arg2.CreatedAt, "###Unavailable$$$");
                }

                history.AddSnapshot(arg2);
                history.Save();
                CommandHandler.RegisterUsage("MessagesChanged", "Messages");
            });

            return Task.CompletedTask;
        }

        private static Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> arg1, ISocketMessageChannel arg2) {
            new Task(async o => {
                try {
                    if (!(arg2 is ITextChannel textChannel)) return;

                    var history = MessageHistory.Get(arg2.Id, arg1.Id);
                    if (history.IsIgnored) {
                        GlobalDB.IgnoredMessages.Delete($"{textChannel.Id}:{arg1.Id}");
                        return;
                    }

                    var guildConfig = GuildConfig.Get(textChannel.GuildId);

                    if (!guildConfig.GetChannel(ChannelFunction.Log, out var logChannel) || logChannel.Id == arg2.Id) return;
                    var loc = new GuildLocalizationProvider(arg2.Id);
                    var embedBuilder = new EmbedBuilder().WithCurrentTimestamp()
                                                         .WithTitle(loc.Get("MessageHistory.MessageWasDeleted"))
                                                         .WithFooter(loc.Get("MessageHistory.MessageId").Format(history.MessageId))
                                                         .AddField(loc.Get("MessageHistory.Channel"), $"<#{history.ChannelId}>", true);
                    if (history.HistoryExists) {
                        embedBuilder.AddField(loc.Get("MessageHistory.Requester"), $"{history.GetAuthor()?.Username} <@{history.AuthorId}>", true);
                        var canFitToEmbed = history.CanFitToEmbed(loc, true);

                        if (canFitToEmbed) {
                            embedBuilder.Fields.InsertRange(0, history.GetEditsAsFields(loc, true));
                            await ((ISocketMessageChannel) logChannel).SendMessageAsync(null, false, embedBuilder.Build());
                        }
                        else {
                            embedBuilder.WithDescription(loc.Get("MessageHistory.LastContentDescription")
                                                            .Format(history.GetLastContent().SafeSubstring(1900, "...")));
                            var logImage = await RenderLog(history);
                            await ((ISocketMessageChannel) logChannel).SendFileAsync(new MemoryStream(logImage),
                                $"History-{history.ChannelId}-{history.MessageId}.jpg",
                                "===========================================", false, embedBuilder.Build());
                        }
                    }
                    else {
                        embedBuilder.WithDescription(loc.Get("MessageHistory.OnDeleteWithoutHistory"));
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
                finally {
                    GlobalDB.Messages.Delete($"{arg2.Id}:{arg1.Id}");
                    CommandHandler.RegisterUsage("MessagesDeleted", "Messages");
                }
            }, TaskCreationOptions.LongRunning).Start();

            return Task.CompletedTask;
        }

        private static Task ClientOnLeftGuild(SocketGuild arg) {
            ClearGuildLogs(arg);

            return Task.CompletedTask;
        }

        private static Task ClientOnChannelDestroyed(SocketChannel arg) {
            if (arg is SocketTextChannel channel) {
                new Task(() => {
                    var deletesCount = GlobalDB.Messages.DeleteMany(history => history.ChannelId == arg.Id);
                    try {
                        var guild = GuildConfig.Get(channel.Guild.Id);
                        if (!guild.GetChannel(ChannelFunction.Log, out var logChannel)) return;
                        var loc = new GuildLocalizationProvider(guild);
                        ((SocketTextChannel) logChannel).SendMessageAsync(loc.Get("MessageHistory.ChannelDeleted").Format(
                            channel.Name, channel.Id, channel.Guild.Name, deletesCount));
                    }
                    finally {
                        logger.Info("Channel {channelName} ({channelId}) on {guild} was deleted. Cleared {messagesCount} messages",
                            channel.Name, channel.Id, channel.Guild.Name, deletesCount);
                    }
                }, TaskCreationOptions.LongRunning).Start();
            }

            return Task.CompletedTask;
        }

        public static Task ClearGuildLogs(SocketGuild arg) {
            new Task(() => {
                var socketGuildChannels = arg.Channels.Where(channel => channel is SocketTextChannel _).ToList();
                var deletesCount = socketGuildChannels.Select(channel => GlobalDB.Messages.DeleteMany(history => channel.Id == history.ChannelId)).Sum();
                try {
                    var guild = GuildConfig.Get(arg.Id);
                    if (!guild.GetChannel(ChannelFunction.Log, out var logChannel)) return;
                    var loc = new GuildLocalizationProvider(guild);
                    ((SocketTextChannel) logChannel).SendMessageAsync(loc.Get("MessageHistory.GuildLogCleared").Format(
                        arg.Name, arg.Id, deletesCount));
                }
                finally {
                    logger.Info("The bot cleared the message history of the guild {guildName} ({guildId}). Cleared {postNumber} posts",
                        arg.Name, arg.Id, deletesCount);
                }
            }, TaskCreationOptions.LongRunning).Start();
            return Task.CompletedTask;
        }

        private static async Task<byte[]> RenderLog(MessageHistory messageHistory) {
            try {
                await ExportHelper.ExportHistoryAsync(messageHistory, Path.Combine(Directory.GetCurrentDirectory(), $"{messageHistory.Id}.html"));
                var converter = new HtmlConverter();
                var bytes = converter.FromHtmlString(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), $"{messageHistory.Id}.html")), 512);
                return bytes;
            }
            finally {
                File.Delete(Path.Combine(Directory.GetCurrentDirectory(), $"{messageHistory.Id}.html"));
            }
        }

        public static async Task PrintLog(MessageHistory history, SocketTextChannel outputChannel, ILocalizationProvider loc, IGuildUser requester) {
            var realMessage = GetRealMessage(history.ChannelId, history.MessageId);
            RestUserMessage logMessage = null;
            var embedBuilder = new EmbedBuilder().WithCurrentTimestamp()
                                                 .WithTitle(loc.Get("MessageHistory.LogTitle"))
                                                 .WithFooter(loc.Get("MessageHistory.MessageId").Format(history.MessageId))
                                                 .AddField(loc.Get("MessageHistory.Requester"), $"<@{requester.Id}>", true)
                                                 .AddField(loc.Get("MessageHistory.Channel"), $"<#{history.ChannelId}>", true);

            if (history.AuthorId != default) {
                embedBuilder.AddField(loc.Get("MessageHistory.Author"), $"{history.GetAuthor()?.Username} (<@{history.AuthorId}>)", true);
            }

            if (history.HistoryExists) {
                if (await realMessage != null) {
                    embedBuilder.Description = loc.Get("MessageHistory.ViewMessageExists").Format((await realMessage).GetJumpUrl());
                }

                var canFitToEmbed = history.CanFitToEmbed(loc, true);

                if (canFitToEmbed) {
                    embedBuilder.Fields.InsertRange(0, history.GetEditsAsFields(loc, true));
                    logMessage = await outputChannel.SendMessageAsync(null, false, embedBuilder.Build());
                }
                else {
                    var logImage = await RenderLog(history);
                    logMessage = await outputChannel.SendFileAsync(new MemoryStream(logImage), $"History-{history.ChannelId}-{history.MessageId}.jpg",
                        null, false, embedBuilder.Build());
                }
            }
            else {
                if (await realMessage != null) {
                    if (history.IsIgnored) {
                        embedBuilder.WithDescription(loc.Get("MessageHistory.MessageIgnore").Format((await realMessage)?.GetJumpUrl()));
                    }
                    else {
                        embedBuilder.WithDescription(loc.Get("MessageHistory.MessageWithoutHistory").Format((await realMessage).GetJumpUrl()));
                        if (Program.Client.GetChannel(history.ChannelId) is SocketGuildChannel guildChannel)
                            LogCreatedMessage(await realMessage, GuildConfig.Get(guildChannel.Guild.Id));
                    }
                }
                else {
                    embedBuilder.WithDescription(loc.Get("MessageHistory.MessageNull"));
                }
                
                logMessage = await outputChannel.SendMessageAsync(null, false, embedBuilder.Build());
            }

            logMessage?.DelayedDelete(TimeSpan.FromMinutes(10));

            async Task<IUserMessage> GetRealMessage(ulong channelId, ulong messageId) {
                try {
                    var textChannel = (ITextChannel) Program.Client.GetChannel(channelId);
                    var messageAsync = await textChannel?.GetMessageAsync(messageId);
                    return messageAsync as IUserMessage;
                }
                catch (Exception) {
                    return null;
                }
            }
        }

        public static void LogCreatedMessage(IMessage arg, GuildConfig config) {
            if (!config.IsLoggingEnabled) {
                AddMessageToIgnore(arg);
                return;
            }

            if (arg.Author.IsBot || arg.Author.IsWebhook) {
                AddMessageToIgnore(arg);
                return;
            }

            if (!(arg.Channel is ITextChannel textChannel)) return;

            new MessageHistory {
                AuthorId = arg.Author.Id,
                ChannelId = textChannel.Id, MessageId = arg.Id,
                Edits = new List<MessageHistory.MessageSnapshot> {
                    new MessageHistory.MessageSnapshot
                        {EditTimestamp = arg.CreatedAt, Value = global::DiffMatchPatch.DiffMatchPatch.patch_toText(DiffMatchPatch.patch_make("", arg.Content))}
                }
            }.Save();
            CommandHandler.RegisterUsage("MessagesCreated", "Messages");
        }

        public static void AddMessageToIgnore(IMessage arg) {
            var id = $"{arg.Channel.Id}:{arg.Id}";
            GlobalDB.IgnoredMessages.Insert(id, new BsonDocument());
        }
    }
}