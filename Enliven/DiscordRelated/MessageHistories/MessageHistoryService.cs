using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Bot.Config.Emoji;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Entities;
using Common.Localization.Providers;
using Common.Utils;
using Discord;
using Discord.WebSocket;
using NLog;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated.MessageHistories {
    public class MessageHistoryService : IService {
        private readonly ILogger _logger;
        private readonly MessageHistoryProvider _messageHistoryProvider;
        private readonly IGuildConfigProvider _guildConfigProvider;
        private readonly IStatisticsPartProvider _statisticsPartProvider;
        private readonly EnlivenShardedClient _enliven;
        private readonly MessageHistoryPrinter _messageHistoryPrinter;
        private readonly MessageHistoryPackPrinter _messageHistoryPackPrinter;
        private readonly CollectorService _collectorService;
        public MessageHistoryService(ILogger logger,
                                     MessageHistoryProvider messageHistoryProvider,
                                     IGuildConfigProvider guildConfigProvider,
                                     IStatisticsPartProvider statisticsPartProvider,
                                     EnlivenShardedClient enliven,
                                     MessageHistoryPrinter messageHistoryPrinter,
                                     MessageHistoryPackPrinter messageHistoryPackPrinter,
                                     CollectorService collectorService) {
            _messageHistoryPackPrinter = messageHistoryPackPrinter;
            _collectorService = collectorService;
            _enliven = enliven;
            _messageHistoryPrinter = messageHistoryPrinter;
            _statisticsPartProvider = statisticsPartProvider;
            _guildConfigProvider = guildConfigProvider;
            _messageHistoryProvider = messageHistoryProvider;
            this._logger = logger;
        }

        public Task OnPostDiscordStartInitialize() {
            // Message created handled located in CommandHandler
            _enliven.MessageUpdated += ClientOnMessageUpdated;
            _enliven.MessageDeleted += ClientOnMessageDeleted;
            _collectorService.CollectReaction(CommonEmoji.LegacyBook,
                reaction => reaction.Channel is ITextChannel textChannel && _guildConfigProvider.Get(textChannel.GuildId).IsLoggingEnabled,
                OnLogEmoteReceived, CollectorFilter.IgnoreBots);
            return Task.CompletedTask;
        }

        private async void OnLogEmoteReceived(EmoteCollectorEventArgs eventArgs) {
            await eventArgs.RemoveReason();
            var reactionChannel = eventArgs.Reaction.Channel as ITextChannel;
            var guildConfig = _guildConfigProvider.Get(reactionChannel!.GuildId);
            try {
                await PrintLog(eventArgs.Reaction.Channel.Id, eventArgs.Reaction.MessageId, reactionChannel, guildConfig.Loc, (IGuildUser)eventArgs.Reaction.User.Value);
            }
            catch (Exception e) {
                _logger.Error(e, "Faled to print log");
            }
        }

        private Task ClientOnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage message, ISocketMessageChannel arg3) {
            Task.Run(() => {
                if (message.Channel is not ITextChannel textChannel) return;
                var history = _messageHistoryProvider.Get(message);
                if (history == null && !NeedLogMessage(message, _guildConfigProvider.Get(textChannel.GuildId), null)) return;
                _messageHistoryProvider.GetAndSync(message);
                _statisticsPartProvider.RegisterUsage("MessagesChanged", "Messages");
            });

            return Task.CompletedTask;
        }

        private Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> messageCacheable, Cacheable<IMessageChannel, ulong> channelCacheable) {
            new Task(async _ => {
                try {
                    var channel = await channelCacheable.GetOrDownloadAsync();
                    if (channel is not ITextChannel textChannel) return;

                    var history = _messageHistoryProvider.Get(channel.Id, messageCacheable.Id);
                    var guildConfig = _guildConfigProvider.Get(textChannel.GuildId);
                    if (!guildConfig.IsLoggingEnabled) return;

                    if (!guildConfig.GetChannel(ChannelFunction.Log, out var logChannelId) || logChannelId == channel.Id) return;
                    if (!guildConfig.LoggedChannels.Contains(textChannel.Id)) return;

                    var logChannel = _enliven.GetChannel(logChannelId) as ITextChannel;
                    if (logChannel == null) return;
                    var logPermissions = await _enliven
                        .GetGuildAsync(textChannel.GuildId)
                        .PipeAsync(guild => guild!.GetUserAsync(_enliven.CurrentUser.Id))
                        .PipeAsync(user => user.GetPermissions(logChannel));
                    if (!logPermissions.SendMessages) return;

                    var loc = guildConfig.Loc;
                    var data = history switch {
                        not null                                      => _messageHistoryPrinter.GenerateDataForDeleted(history, loc, guildConfig.MessageExportType),
                        null when guildConfig.SendWithoutHistoryPacks => _messageHistoryPackPrinter.GeneratePack(textChannel, loc),
                        null when guildConfig.HistoryMissingInLog     => _messageHistoryPrinter.GenerateDataForDeletedWithoutHistory(textChannel, messageCacheable.Id, loc),
                        _                                             => null
                    };

                    if (data == null) return;
                    await data.SendMessage(logChannel);
                    _statisticsPartProvider.RegisterUsage("MessagesDeleted", "Messages");
                }
                catch (Exception e) {
                    _logger.Error(e, "Failed to print log message");
                }
                finally {
                    _messageHistoryProvider.Delete($"{channelCacheable.Id}:{messageCacheable.Id}");
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
                    ((SocketTextChannel)logChannel)!.SendMessageAsync(loc.Get("MessageHistory.GuildLogCleared").Format(
                        arg.Name, arg.Id, deletesCount));
                }
                finally {
                    _logger.Info("The bot cleared the message history of the guild {guildName} ({guildId}). Cleared {postNumber} posts",
                        arg.Name, arg.Id, deletesCount);
                }
            }, TaskCreationOptions.LongRunning).Start();
            return Task.CompletedTask;
        }

        public Task PrintLog(ulong channelId, ulong messageId, IMessageChannel outputChannel, ILocalizationProvider loc, IGuildUser requester,
                             bool forceImage = false) {
            return PrintLog(channelId, messageId, null, outputChannel, loc, requester, forceImage);
        }

        public Task PrintLog(IUserMessage message, IMessageChannel outputChannel, ILocalizationProvider loc, IGuildUser requester,
                             bool forceImage = false) {
            return PrintLog(message.Channel.Id, message.Id, message, outputChannel, loc, requester, forceImage);
        }

        public Task PrintLog(ulong channelId, ulong messageId, IUserMessage? message, IMessageChannel outputChannel, ILocalizationProvider loc, IGuildUser requester,
                             bool forceImage = false) {
            var messageHistory = _messageHistoryProvider.Get(channelId, messageId);
            return PrintLog(messageHistory, message, outputChannel, loc, requester, forceImage);
        }

        public async Task PrintLog(MessageHistory? history, IUserMessage? message, IMessageChannel outputChannel, ILocalizationProvider loc, IGuildUser requester,
                                   bool forceImage = false) {
            if (history == null && message != null) {
                var guildConfig = (message.Channel as IGuildChannel)?.Guild.Id.Pipe(_guildConfigProvider.Get);
                if (guildConfig != null) history = TryLogCreatedMessage(message, guildConfig, null);
            }
            var logMessage = await _messageHistoryPrinter
                .GenerateDataForLog(history, forceImage, loc, requester, message)
                .SendMessage(outputChannel);
            _ = logMessage.DelayedDelete(Constants.LongTimeSpan);
        }

        public bool NeedLogMessage(IMessage arg, GuildConfig config, bool? isCommand) {
            if (!config.IsLoggingEnabled || arg.Author.IsBot || arg.Author.IsWebhook) return false;
            if (!(arg.Channel is ITextChannel textChannel)) return false;
            if (isCommand == true && !config.IsCommandLoggingEnabled) return false;

            return config.LoggedChannels.Contains(textChannel.Id);
        }

        public MessageHistory? TryLogCreatedMessage(IMessage arg, GuildConfig config, bool? isCommand) {
            if (!NeedLogMessage(arg, config, isCommand))
                return null;

            var history = _messageHistoryProvider.GetAndSync(arg);
            history.Save();
            _statisticsPartProvider.RegisterUsage("MessagesCreated", "Messages");
            return history;
        }

        public static string GetAttachmentString(MessageHistory history) {
            if (history.Attachments == null) return "";
            return history.Attachments
                .Select(s => DiscordHelper.ParseAttachmentFromUrlAsync(s, DiscordHelper.NeverFetch).GetAwaiter().GetResult())
                .Select(attachment => $"[{attachment.Filename}]({attachment.Url})")
                .JoinToString("\n");
        }
    }
}