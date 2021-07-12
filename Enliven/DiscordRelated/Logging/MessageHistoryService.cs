using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Config.Emoji;
using Common.Localization.Providers;
using Discord;
using Discord.WebSocket;
using GrapeCity.Documents.Html;
using HarmonyLib;
using NLog;

namespace Bot.DiscordRelated.Logging {
    public class MessageHistoryService : IService {
        public static readonly Utilities.DiffMatchPatch.DiffMatchPatch DiffMatchPatch = new Utilities.DiffMatchPatch.DiffMatchPatch();
        public MessageHistoryService(ILogger logger, IMessageHistoryProvider messageHistoryProvider, IGuildConfigProvider guildConfigProvider, 
                                     IStatisticsPartProvider statisticsPartProvider) {
            _statisticsPartProvider = statisticsPartProvider;
            _guildConfigProvider = guildConfigProvider;
            _messageHistoryProvider = messageHistoryProvider;
            this.logger = logger;
        }
        
        public Task OnPostDiscordStartInitialize() {
            // Message created handled located in CommandHandler
            EnlivenBot.Client.MessageUpdated += ClientOnMessageUpdated;
            EnlivenBot.Client.MessageDeleted += ClientOnMessageDeleted;
            CollectorsUtils.CollectReaction(CommonEmoji.LegacyBook, reaction => {
                if (!(reaction.Channel is ITextChannel textChannel)) return false;
                return _guildConfigProvider.Get(textChannel.GuildId).IsLoggingEnabled;
            }, async eventArgs => {
                await eventArgs.RemoveReason();
                var reactionChannel = eventArgs.Reaction.Channel as ITextChannel;
                var guildConfig = _guildConfigProvider.Get(reactionChannel!.GuildId);
                try {
                    await PrintLog(_messageHistoryProvider.Get(eventArgs.Reaction.Channel.Id, eventArgs.Reaction.MessageId),
                        reactionChannel, guildConfig.Loc, (IGuildUser) eventArgs.Reaction.User.Value);
                }
                catch (Exception e) {
                    LogManager.GetCurrentClassLogger().Error(e, "Faled to print log");
                }
            }, CollectorFilter.IgnoreBots);

            // Sorry for this hack
            // But this project does not bring me income, and I can not afford to buy this license
            // If you using it consider buying license at https://www.grapecity.com/documents-api/licensing
            var type = typeof(GcHtmlRenderer).Assembly.GetType("aov");
            AccessTools.Field(type, "c").SetValue(null, int.MaxValue);
            
            return Task.CompletedTask;
        }

        private Task ClientOnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3) {
            Task.Run(() => {
                if (!(arg2.Channel is ITextChannel textChannel)) return;
                var history = _messageHistoryProvider.Get(arg2);
                if (!history.HistoryExists) {
                    if (!NeedLogMessage(arg2, _guildConfigProvider.Get(textChannel.GuildId), null)) return;
                    history = _messageHistoryProvider.FromMessage(arg2);
                    history.IsHistoryUnavailable = true;
                }
                else {
                    history.AddSnapshot(arg2);
                }

                history.Save();
                _statisticsPartProvider.RegisterUsage("MessagesChanged", "Messages");
            });

            return Task.CompletedTask;
        }

        private ConcurrentDictionary<ulong, SemaphoreSlim> _packSemaphores = new ConcurrentDictionary<ulong, SemaphoreSlim>();
        private ILogger logger;
        private IMessageHistoryProvider _messageHistoryProvider;
        private IGuildConfigProvider _guildConfigProvider;
        private IStatisticsPartProvider _statisticsPartProvider;

        private Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> arg1, ISocketMessageChannel arg2) {
            new Task(async o => {
                try {
                    if (!(arg2 is ITextChannel textChannel)) return;

                    var history = _messageHistoryProvider.Get(arg2.Id, arg1.Id);
                    var guild = EnlivenBot.Client.GetGuild(textChannel.GuildId);
                    var guildConfig = _guildConfigProvider.Get(textChannel.GuildId);
                    if (!guildConfig.IsLoggingEnabled) return;

                    if (!guildConfig.GetChannel(ChannelFunction.Log, out var logChannelId) || logChannelId == arg2.Id) return;
                    var logChannel = EnlivenBot.Client.GetChannel(logChannelId);
                    if (!guildConfig.LoggedChannels.Contains(textChannel.Id)) return;

                    var logPermissions = guild.GetUser(EnlivenBot.Client.CurrentUser.Id).GetPermissions((IGuildChannel) logChannel);
                    if (!logPermissions.SendMessages) return;

                    var loc = guildConfig.Loc;
                    var embedBuilder = new EmbedBuilder().WithCurrentTimestamp()
                                                         .WithTitle(loc.Get("MessageHistory.MessageWasDeleted"))
                                                         .WithFooter(loc.Get("MessageHistory.MessageId").Format(history.MessageId))
                                                         .AddField(loc.Get("MessageHistory.Channel"), $"<#{history.ChannelId}>", true);
                    if (history.HistoryExists) {
                        if (history.HasAttachments) {
                            embedBuilder.AddField(loc.Get("MessageHistory.AttachmentsTitle"), await history.GetAttachmentsString());
                        }

                        embedBuilder.AddField(loc.Get("MessageHistory.Author"), $"{history.GetAuthor()?.Username} <@{history.AuthorId}>", true);

                        if (history.CanFitToEmbed(loc)) {
                            embedBuilder.Fields.InsertRange(0, history.GetEditsAsFields(loc));
                            await ((ISocketMessageChannel) logChannel).SendMessageAsync(null, false, embedBuilder.Build());
                        }
                        else {
                            if (!logPermissions.AttachFiles) return;
                            embedBuilder.WithDescription(loc.Get("MessageHistory.LastContentDescription")
                                                            .Format(history.GetLastContent().SafeSubstring(1900, "...")));
                            var historyHtml = await history.ExportToHtml(loc);
                            var uploadStream = guildConfig.LogExportType switch {
                                LogExportTypes.Html  => new MemoryStream(Encoding.UTF8.GetBytes(historyHtml)),
                                LogExportTypes.Image => RenderLog(historyHtml),
                                _                    => throw new SwitchExpressionException(guildConfig.LogExportType)
                            };
                            var fileName = guildConfig.LogExportType switch {
                                LogExportTypes.Html  => $"History-{history.ChannelId}-{history.MessageId}.html",
                                LogExportTypes.Image => $"History-{history.ChannelId}-{history.MessageId}.png",
                                _                    => throw new SwitchExpressionException(guildConfig.LogExportType)
                            };
                            await ((ISocketMessageChannel) logChannel).SendFileAsync(uploadStream, fileName,
                                "===========================================", false, embedBuilder.Build());
                        }

                        _statisticsPartProvider.RegisterUsage("MessagesDeleted", "Messages");
                    }
                    else if (guildConfig.HistoryMissingInLog) {
                        if (guildConfig.HistoryMissingPacks) {
                            await _packSemaphores.GetOrAdd(guildConfig.GuildId, new SemaphoreSlim(1)).WaitAsync();
                            try {
                                IUserMessage? packMessage = await Common.Utilities.TryAsync(async () => {
                                    var firstOrDefault = (await ((logChannel as ITextChannel)!).GetMessagesAsync(1).FlattenAsync()).FirstOrDefault();
                                    if (firstOrDefault.Author.Id != EnlivenBot.Client.CurrentUser.Id) return null;
                                    if (!firstOrDefault.Embeds.First().Title.Contains("Pack")) return null;
                                    return (IUserMessage) firstOrDefault;
                                }, e => null);
                                if (packMessage == null) {
                                    await SendPackMessage();
                                }
                                else {
                                    try {
                                        var packBuilder = new EmbedBuilder().WithTitle("Deleted messages Pack")
                                                                            .WithDescription(packMessage.Embeds.First().Description);
                                        packBuilder.Description += $"\n{DateTimeOffset.UtcNow} in {textChannel.Mention}";
                                        await packMessage.ModifyAsync(properties => properties.Embed = packBuilder.Build());
                                    }
                                    catch (Exception) {
                                        await SendPackMessage();
                                    }
                                }

                                async Task SendPackMessage() {
                                    var packBuilder = new EmbedBuilder().WithTitle("Deleted messages Pack")
                                                                        .WithDescription(loc.Get("MessageHistory.DeletedMessagesPackDescription"));
                                    packBuilder.Description += $"\n{DateTimeOffset.UtcNow} in {textChannel.Mention}";
                                    await (logChannel as ITextChannel)!.SendMessageAsync(null, false, packBuilder.Build());
                                }
                            }
                            finally {
                                _packSemaphores[guildConfig.GuildId].Release();
                            }
                        }
                        else {
                            embedBuilder.AddField(loc.Get("MessageHistory.LastContent"), loc.Get("MessageHistory.Unavailable"));
                            await ((ISocketMessageChannel) logChannel).SendMessageAsync("===========================================", false,
                                embedBuilder.Build());
                        }

                        _statisticsPartProvider.RegisterUsage("MessagesDeleted", "Messages");
                    }
                }
                catch (Exception e) {
                    logger.Error(e, "Failed to print log message");
                }
                finally {
                    _messageHistoryProvider.Delete($"{arg2.Id}:{arg1.Id}");
                }
            }, TaskCreationOptions.LongRunning).Start();

            return Task.CompletedTask;
        }

        public Task ClearGuildLogs(SocketGuild arg) {
            new Task(() => {
                var socketGuildChannels = arg.Channels.Where(channel => channel is SocketTextChannel _).ToList();
                var deletesCount = socketGuildChannels.Select(channel => _messageHistoryProvider.DeleteMany(history => channel.Id == history.ChannelId)).Sum();
                try {
                    var guild = _guildConfigProvider.Get(arg.Id);
                    if (!guild.GetChannel(ChannelFunction.Log, out var logChannelId)) return;
                    var loc = new GuildLocalizationProvider(guild);
                    var logChannel = EnlivenBot.Client.GetChannel(logChannelId);
                    ((SocketTextChannel) logChannel)!.SendMessageAsync(loc.Get("MessageHistory.GuildLogCleared").Format(
                        arg.Name, arg.Id, deletesCount));
                }
                finally {
                    logger.Info("The bot cleared the message history of the guild {guildName} ({guildId}). Cleared {postNumber} posts",
                        arg.Name, arg.Id, deletesCount);
                }
            }, TaskCreationOptions.LongRunning).Start();
            return Task.CompletedTask;
        }

        private MemoryStream RenderLog(string html) {
            using var re1 = new GcHtmlRenderer(html);
            var pngSettings = new PngSettings {FullPage = true, WindowSize = new Size(512, 1)};

            var stream = new MemoryStream();
            re1.RenderToPng(stream, pngSettings);
            stream.Position = 0;
            return stream;
        }

        public async Task PrintLog(MessageHistory history, ITextChannel outputChannel, ILocalizationProvider loc, IGuildUser requester,
                                          bool forceImage = false) {
            var realMessage = history.GetRealMessage();
            IUserMessage? logMessage = null;
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
                    var logImage = RenderLog(await history.ExportToHtml(loc));
                    logMessage = await outputChannel.SendFileAsync(logImage, $"History-{history.ChannelId}-{history.MessageId}.png",
                        null, false, embedBuilder.Build());
                }
            }
            else {
                if (await realMessage != null) {
                    embedBuilder.WithDescription(loc.Get("MessageHistory.MessageWithoutHistory").Format((await realMessage).GetJumpUrl()));
                    if (EnlivenBot.Client.GetChannel(history.ChannelId) is SocketGuildChannel guildChannel)
                        TryLogCreatedMessage((await realMessage)!, _guildConfigProvider.Get(guildChannel.Guild.Id), null);
                }
                else {
                    embedBuilder.WithDescription(loc.Get("MessageHistory.MessageNull"));
                }

                logMessage = await outputChannel.SendMessageAsync(null, false, embedBuilder.Build());
            }

            logMessage?.DelayedDelete(Constants.LongTimeSpan);
        }

        public bool NeedLogMessage(IMessage arg, GuildConfig config, bool? isCommand) {
            if (!config.IsLoggingEnabled || arg.Author.IsBot || arg.Author.IsWebhook) return false;
            if (!(arg.Channel is ITextChannel textChannel)) return false;
            if (isCommand == true && !config.IsCommandLoggingEnabled) return false;

            return config.LoggedChannels.Contains(textChannel.Id);
        }

        public void TryLogCreatedMessage(IMessage arg, GuildConfig config, bool? isCommand) {
            if (!NeedLogMessage(arg, config, isCommand))
                return;

            var history = _messageHistoryProvider.FromMessage(arg);
            history.Save();
            _statisticsPartProvider.RegisterUsage("MessagesCreated", "Messages");
        }
    }
}