using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization.Providers;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.Commands;
using Bot.Utilities.Emoji;
using Discord;
using Discord.WebSocket;
using GrapeCity.Documents.Html;
using HarmonyLib;
using NLog;

namespace Bot.Logging {
    public static class MessageHistoryManager {

        // ReSharper disable once InconsistentNaming
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public static readonly DiffMatchPatch.DiffMatchPatch DiffMatchPatch = new DiffMatchPatch.DiffMatchPatch();

        static MessageHistoryManager() {
            if (Program.CmdOptions.Observer) return;
            // Message created handled located in CommandHandler
            Program.Client.MessageUpdated += ClientOnMessageUpdated;
            Program.Client.MessageDeleted += ClientOnMessageDeleted;
            CollectorsUtils.CollectReaction(CommonEmoji.LegacyBook, reaction => {
                if (!(reaction.Channel is ITextChannel textChannel)) return false;
                return GuildConfig.Get(textChannel.GuildId).IsLoggingEnabled;
            }, async eventArgs => {
                await eventArgs.RemoveReason();
                var reactionChannel = eventArgs.Reaction.Channel as ITextChannel;
                var guildConfig = GuildConfig.Get(reactionChannel!.GuildId);
                try {
                    await PrintLog(MessageHistory.Get(eventArgs.Reaction.Channel.Id, eventArgs.Reaction.MessageId),
                        reactionChannel, guildConfig.Loc, (IGuildUser) eventArgs.Reaction.User.Value);
                }
                catch (Exception e) {
                    logger.Error(e, "Faled to print log");
                }
            }, CollectorFilter.IgnoreBots);

            // Sorry for this hack
            // But this project does not bring me income, and I can not afford to buy this license
            // If you using it consider buying license at https://www.grapecity.com/documents-api/licensing
            var type = typeof(GcHtmlRenderer).Assembly.GetType("anz");
            AccessTools.Field(type, "c").SetValue(null, int.MaxValue);
        }

        public static void Initialize() {
            // Dummy method to initialize static properties
        }

        private static Task ClientOnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3) {
            Task.Run(() => {
                if (!(arg2.Channel is ITextChannel textChannel)) return;
                var history = MessageHistory.Get(arg2);
                if (!history.HistoryExists) {
                    if (!NeedLogMessage(arg2, GuildConfig.Get(textChannel.GuildId), null)) return;
                    history = MessageHistory.FromMessage(arg2);
                    history.IsHistoryUnavailable = true;
                }
                else {
                    history.AddSnapshot(arg2);
                }

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
                    var guildConfig = GuildConfig.Get(textChannel.GuildId);

                    if (!guildConfig.GetChannel(ChannelFunction.Log, out var logChannel) || logChannel.Id == arg2.Id) return;
                    var loc = guildConfig.Loc;
                    var embedBuilder = new EmbedBuilder().WithCurrentTimestamp()
                                                         .WithTitle(loc.Get("MessageHistory.MessageWasDeleted"))
                                                         .WithFooter(loc.Get("MessageHistory.MessageId").Format(history.MessageId))
                                                         .AddField(loc.Get("MessageHistory.Channel"), $"<#{history.ChannelId}>", true);
                    if (history.HistoryExists) {
                        if (history.HasAttachments) {
                            embedBuilder.AddField(loc.Get("MessageHistory.AttachmentsTitle"), await history.GetAttachmentsString());
                        }

                        embedBuilder.AddField(loc.Get("MessageHistory.Requester"), $"{history.GetAuthor()?.Username} <@{history.AuthorId}>", true);

                        if (history.CanFitToEmbed(loc)) {
                            embedBuilder.Fields.InsertRange(0, history.GetEditsAsFields(loc));
                            await ((ISocketMessageChannel) logChannel).SendMessageAsync(null, false, embedBuilder.Build());
                        }
                        else {
                            embedBuilder.WithDescription(loc.Get("MessageHistory.LastContentDescription")
                                                            .Format(history.GetLastContent().SafeSubstring(1900, "...")));
                            var historyHtml = await history.ExportToHtml(loc);
                            var uploadStream = guildConfig.LogExportType switch {
                                LogExportTypes.Html  => new MemoryStream(Encoding.UTF8.GetBytes(historyHtml)),
                                LogExportTypes.Image => RenderLog(historyHtml, history)
                            };
                            var fileName = guildConfig.LogExportType switch {
                                LogExportTypes.Html  => $"History-{history.ChannelId}-{history.MessageId}.html",
                                LogExportTypes.Image => $"History-{history.ChannelId}-{history.MessageId}.png"
                            };
                            await ((ISocketMessageChannel) logChannel).SendFileAsync(uploadStream, fileName,
                                "===========================================", false, embedBuilder.Build());
                        }
                        
                        CommandHandler.RegisterUsage("MessagesDeleted", "Messages");
                    }
                    else if (guildConfig.HistoryMissingInLog)
                    {
                        embedBuilder.AddField(loc.Get("MessageHistory.LastContent"),loc.Get("MessageHistory.Unavailable"));
                        await ((ISocketMessageChannel) logChannel).SendMessageAsync("===========================================", false,
                            embedBuilder.Build());
                        
                        CommandHandler.RegisterUsage("MessagesDeleted", "Messages");
                    }
                }
                catch (Exception e) {
                    logger.Error(e, "Failed to print log message");
                }
                finally {
                    GlobalDB.Messages.Delete($"{arg2.Id}:{arg1.Id}");
                }
            }, TaskCreationOptions.LongRunning).Start();

            return Task.CompletedTask;
        }
        //
        // private static Task ClientOnLeftGuild(SocketGuild arg) {
        //     ClearGuildLogs(arg);
        //
        //     return Task.CompletedTask;
        // }
        //
        // private static Task ClientOnChannelDestroyed(SocketChannel arg) {
        //     if (arg is SocketTextChannel channel) {
        //         new Task(() => {
        //             var deletesCount = GlobalDB.Messages.DeleteMany(history => history.ChannelId == arg.Id);
        //             try {
        //                 var guild = GuildConfig.Get(channel.Guild.Id);
        //                 if (!guild.GetChannel(ChannelFunction.Log, out var logChannel)) return;
        //                 var loc = new GuildLocalizationProvider(guild);
        //                 ((SocketTextChannel) logChannel).SendMessageAsync(loc.Get("MessageHistory.ChannelDeleted").Format(
        //                     channel.Name, channel.Id, channel.Guild.Name, deletesCount));
        //             }
        //             finally {
        //                 logger.Info("Channel {channelName} ({channelId}) on {guild} was deleted. Cleared {messagesCount} messages",
        //                     channel.Name, channel.Id, channel.Guild.Name, deletesCount);
        //             }
        //         }, TaskCreationOptions.LongRunning).Start();
        //     }
        //
        //     return Task.CompletedTask;
        // }

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

        private static MemoryStream RenderLog(string html, MessageHistory messageHistory) {
            using var re1 = new GcHtmlRenderer(html);
            var pngSettings = new PngSettings {FullPage = true, WindowSize = new Size(512, 1)};

            var stream = new MemoryStream();
            re1.RenderToPng(stream, pngSettings);
            stream.Position = 0;
            return stream;
        }

        public static async Task PrintLog(MessageHistory history, ITextChannel outputChannel, ILocalizationProvider loc, IGuildUser requester, bool forceImage = false) {
            var realMessage = history.GetRealMessage();
            IUserMessage logMessage = null;
            var embedBuilder = new EmbedBuilder().WithCurrentTimestamp()
                                                 .WithTitle(loc.Get("MessageHistory.LogTitle"))
                                                 .WithFooter(loc.Get("MessageHistory.MessageId").Format(history.MessageId))
                                                 .AddField(loc.Get("MessageHistory.Requester"), $"<@{requester.Id}>", true)
                                                 .AddField(loc.Get("MessageHistory.Channel"), $"<#{history.ChannelId}>", true);

            if (history.AuthorId != default) {
                embedBuilder.AddField(loc.Get("MessageHistory.Author"), $"{history.GetAuthor()?.Username} (<@{history.AuthorId}>)", true);
            }

            if (history.HistoryExists) {
                if (history.HasAttachments) {
                    embedBuilder.AddField(loc.Get("MessageHistory.AttachmentsTitle"), await history.GetAttachmentsString());
                }

                if (await realMessage != null) {
                    embedBuilder.Description = loc.Get("MessageHistory.ViewMessageExists").Format((await realMessage).GetJumpUrl());
                }

                if (!forceImage && history.CanFitToEmbed(loc)) {
                    embedBuilder.Fields.InsertRange(0, history.GetEditsAsFields(loc));
                    logMessage = await outputChannel.SendMessageAsync(null, false, embedBuilder.Build());
                }
                else {
                    var logImage = RenderLog(await history.ExportToHtml(loc), history);
                    logMessage = await outputChannel.SendFileAsync(logImage, $"History-{history.ChannelId}-{history.MessageId}.png",
                        null, false, embedBuilder.Build());
                }
            }
            else {
                if (await realMessage != null) {
                    embedBuilder.WithDescription(loc.Get("MessageHistory.MessageWithoutHistory").Format((await realMessage).GetJumpUrl()));
                    if (Program.Client.GetChannel(history.ChannelId) is SocketGuildChannel guildChannel)
                        TryLogCreatedMessage((await realMessage)!, GuildConfig.Get(guildChannel.Guild.Id), null);
                }
                else {
                    embedBuilder.WithDescription(loc.Get("MessageHistory.MessageNull"));
                }

                logMessage = await outputChannel.SendMessageAsync(null, false, embedBuilder.Build());
            }

            logMessage?.DelayedDelete(TimeSpan.FromMinutes(10));
        }

        public static bool NeedLogMessage(IMessage arg, GuildConfig config, bool? isCommand) {
            if (!config.IsLoggingEnabled || arg.Author.IsBot || arg.Author.IsWebhook) return false;
            if (!(arg.Channel is ITextChannel textChannel)) return false;
            if (isCommand == true && !config.IsCommandLoggingEnabled) return false;

            return config.LoggedChannels.Contains(textChannel.Id);
        }

        public static void TryLogCreatedMessage(IMessage arg, GuildConfig config, bool? isCommand) {
            if (!NeedLogMessage(arg, config, isCommand))
                return;

            var history = MessageHistory.FromMessage(arg);
            history.Save();
            CommandHandler.RegisterUsage("MessagesCreated", "Messages");
        }
    }
}