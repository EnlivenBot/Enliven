using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.Config.Emoji;
using Bot.DiscordRelated;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Discord;

namespace Bot.Commands.Chains {
    public class LoggingChain : ChainBase {
        private static readonly Regex ChannelRegex = new Regex(@"^<#(\d{18})>$");
        private readonly GuildConfig _guildConfig;
        private readonly CollectorService _collectorService;
        private readonly IUser _user;
        private readonly ITextChannel _channel;
        private IUserMessage? _message;
        private CollectorsGroup? _collectorsGroup;

        public LoggingChain(ITextChannel channel, IUser user, GuildConfig guildConfig, CollectorService collectorService)
            : base($"{nameof(LoggingChain)}_{guildConfig.GuildId}", guildConfig.Loc) {
            _channel = channel;
            _user = user;
            _guildConfig = guildConfig;
            _collectorService = collectorService;
            MainBuilder = DiscordUtils
                .GetAuthorEmbedBuilderWrapper(user, guildConfig.Loc)
                .WithColor(Color.Gold)
                .WithTitle(guildConfig.Loc.Get("Chains.LoggingTitle"));
        }

        public async Task Start() {
            var functionalChannelsChangedSubscribe = _guildConfig.FunctionalChannelsChanged.Subscribe(_ => Update());
            Update();
            _message = await _channel.SendMessageAsync(null, false, MainBuilder.Build());
            _collectorsGroup = new CollectorsGroup(_collectorService.CollectMessage(_user, message => message.Channel.Id == _message.Channel.Id, async args => {
                    var match = ChannelRegex.Match(args.Message.Content.Trim());
                    if (!match.Success) return;
                    SetTimeout(Constants.StandardTimeSpan);
                    var targetChannel = EnlivenBot.Client.GetChannel(Convert.ToUInt64(match.Groups[1].Value));
                    if (targetChannel is not ITextChannel targetTextChannel || targetTextChannel.Guild.Id != _guildConfig.GuildId) return;
                    _guildConfig.ToggleChannelLogging(targetChannel.Id);
                    _guildConfig.Save();

                    Update();

                    await args.RemoveReason();
                }),
                _collectorService.CollectReaction(_message, reaction => reaction.UserId == _user.Id,
                    async args => {
                        if (args.Reaction.Emote.Equals(CommonEmoji.Memo)) {
                            _guildConfig.IsLoggingEnabled = !_guildConfig.IsLoggingEnabled;
                        }
                        else if (args.Reaction.Emote.Equals(CommonEmoji.Robot)) {
                            _guildConfig.IsCommandLoggingEnabled = !_guildConfig.IsCommandLoggingEnabled;
                        }
                        else if (args.Reaction.Emote.Equals(CommonEmoji.ExclamationPoint)) {
                            _guildConfig.HistoryMissingInLog = !_guildConfig.HistoryMissingInLog;
                        }
                        else if (args.Reaction.Emote.Equals(CommonEmoji.LegacyStop)) {
                            OnEnd.Invoke(new EntryLocalized("ChainsCommon.Thanks").Add(() => _guildConfig.Prefix));
                            return;
                        }
                        else if (args.Reaction.Emote.Equals(CommonEmoji.LegacyFileBox)) {
                            _guildConfig.HistoryMissingPacks = !_guildConfig.HistoryMissingPacks;
                        }
                        else if (args.Reaction.Emote.Equals(CommonEmoji.Printer)) {
                            _guildConfig.MessageExportType = _guildConfig.MessageExportType.Next();
                        }
                        else {
                            return;
                        }

                        _guildConfig.Save();
                        Update();
                        await args.RemoveReason();
                    }));

            await _message.AddReactionsAsync(new IEmote[]
                { CommonEmoji.Memo, CommonEmoji.Robot, CommonEmoji.ExclamationPoint, CommonEmoji.LegacyFileBox, CommonEmoji.Printer, CommonEmoji.LegacyStop });
            OnEnd = async entry => {
                _collectorsGroup.DisposeAll();
                functionalChannelsChangedSubscribe.Dispose();
                await _message.ModifyAsync(properties =>
                    properties.Embed = new EmbedBuilder().WithColor(Color.Orange).WithTitle(Loc.Get("ChainsCommon.Ended")).WithDescription(entry.Get(Loc))
                        .Build());
                await _message.RemoveAllReactionsAsync();
                _ = _message.DelayedDelete(Constants.StandardTimeSpan);
            };
            SetTimeout(Constants.StandardTimeSpan);
        }

        public override void Update() {
            var descriptionBuilder = new StringBuilder();
            descriptionBuilder.AppendLine(_guildConfig.IsLoggingEnabled
                ? Loc.Get("Logging.Enabled")
                : Loc.Get("Logging.Disabled"));
            if (_guildConfig.IsLoggingEnabled) {
                descriptionBuilder.AppendLine(_guildConfig.IsCommandLoggingEnabled
                    ? Loc.Get("Logging.CommandEnabled")
                    : Loc.Get("Logging.CommandDisabled"));
                descriptionBuilder.AppendLine(_guildConfig.HistoryMissingInLog
                    ? Loc.Get("Logging.HistoryMissingInLogEnabled")
                    : Loc.Get("Logging.HistoryMissingInLogDisabled"));
                if (_guildConfig.HistoryMissingInLog) {
                    descriptionBuilder.AppendLine(_guildConfig.HistoryMissingPacks
                        ? Loc.Get("Logging.HistoryMissingPacksEnabled")
                        : Loc.Get("Logging.HistoryMissingPacksDisabled"));
                }
            }

            var historyChannelExists = _guildConfig.GetChannel(ChannelFunction.Log, out var logChannelId);
            if (historyChannelExists && _guildConfig.IsLoggingEnabled) {
                descriptionBuilder.AppendLine(_guildConfig.MessageExportType == MessageExportType.Image
                    ? Loc.Get("Logging.OutputToImage")
                    : Loc.Get("Logging.OutputToHtml"));
            }

            descriptionBuilder.AppendLine(Loc.Get("Logging.LogChannel").Format(
                historyChannelExists
                    ? $"<#{logChannelId}>"
                    : Loc.Get("Logging.LogChannelMissing").Format(_guildConfig.Prefix)));
            MainBuilder.Description = descriptionBuilder.ToString();

            MainBuilder.GetOrAddField("info", s => new PriorityEmbedFieldBuilder().WithPriority(100))
                .WithName(Loc.Get("Logging.InfoTitle")).WithValue(Loc.Get("Logging.Info").Format(_channel.Mention));

            var loggedChannels = string.Join("\n", _guildConfig.LoggedChannels.Select(arg => $"<#{arg}>"));
            MainBuilder.GetOrAddField("channelsList").WithName(Loc.Get("Logging.LoggedChannelsTitle"))
                .WithValue(string.IsNullOrWhiteSpace(loggedChannels) ? Loc.Get("Logging.LoggedChannelsEmpty") : loggedChannels);
            try {
                _message?.ModifyAsync(properties => properties.Embed = MainBuilder.Build());
            }
            catch (Exception) {
                // ignored
            }
        }
    }
}