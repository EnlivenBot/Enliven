using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Config.Localization.Providers;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.Emoji;
using Discord;

namespace Bot.Commands.Chains {
    public class LoggingChain : ChainBase {
        private static readonly Regex ChannelRegex = new Regex(@"^<#(\d{18})>$");
        private ILocalizationProvider Loc => _guildConfig.Loc;
        private GuildConfig _guildConfig = null!;
        private IUser _user = null!;
        private ITextChannel _channel = null!;
        private IUserMessage? _message;
        private CollectorsGroup? _collectorsGroup;

        public static LoggingChain CreateInstance(ITextChannel channel, IUser user, GuildConfig guildConfig) {
            var loggingChainBase = new LoggingChain($"{nameof(LoggingChain)}_{guildConfig.GuildId}") {
                _channel = channel, _user = user, _guildConfig = guildConfig, MainBuilder = DiscordUtils.GetAuthorEmbedBuilderWrapper(user, guildConfig.Loc)
            };
            loggingChainBase.MainBuilder.WithColor(Color.Gold).WithTitle(guildConfig.Loc.Get("Chains.LoggingTitle"));
            return loggingChainBase;
        }

        public async Task Start() {
            _guildConfig.FunctionalChannelsChanged += GuildConfigOnFunctionalChannelsChanged;
            UpdateEmbedBuilder();
            _message = await _channel.SendMessageAsync(null, false, MainBuilder.Build());
            _collectorsGroup = new CollectorsGroup(CollectorsUtils.CollectMessage(_user, message => message.Channel.Id == _message.Channel.Id, async args => {
                    var match = ChannelRegex.Match(args.Message.Content.Trim());
                    if (!match.Success) return;
                    SetTimeout(TimeSpan.FromMinutes(3));
                    var targetChannel = Program.Client.GetChannel(Convert.ToUInt64(match.Groups[1].Value));
                    if (!(targetChannel is ITextChannel targetTextChannel) || targetTextChannel.Guild.Id != _guildConfig.GuildId) return;
                    _guildConfig.ToggleChannelLogging(targetChannel.Id);
                    _guildConfig.Save();

                    UpdateEmbedBuilder();
                    _message.ModifyAsync(properties => properties.Embed = MainBuilder.Build());

                    await args.RemoveReason();
                }),
                CollectorsUtils.CollectReaction(_message, reaction => reaction.UserId == _user.Id,
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
                            OnEnd.Invoke(new LocalizedEntry("ChainsCommon.Thanks").Add(() => _guildConfig.Prefix));
                            return;
                        }
                        else if (args.Reaction.Emote.Equals(CommonEmoji.LegacyFileBox)) {
                            _guildConfig.HistoryMissingPacks = !_guildConfig.HistoryMissingPacks;
                        }
                        else if (args.Reaction.Emote.Equals(CommonEmoji.Printer)) {
                            _guildConfig.LogExportType = _guildConfig.LogExportType.Next();
                        }
                        else {
                            return;
                        }

                        _guildConfig.Save();
                        UpdateEmbedBuilder();
                        await _message.ModifyAsync(properties => properties.Embed = MainBuilder.Build());
                        await args.RemoveReason();
                    }));

            await _message.AddReactionsAsync(new[]
                {CommonEmoji.Memo, CommonEmoji.Robot, CommonEmoji.ExclamationPoint, CommonEmoji.LegacyFileBox, CommonEmoji.Printer, CommonEmoji.LegacyStop});
            OnEnd = async entry => {
                await _message.ModifyAsync(properties =>
                    properties.Embed = new EmbedBuilder().WithColor(Color.Orange).WithTitle(Loc.Get("ChainsCommon.Ended")).WithDescription(entry.Get(Loc))
                                                         .Build());
                await _message.RemoveAllReactionsAsync();
                _message.DelayedDelete(TimeSpan.FromMinutes(5));
                _collectorsGroup.DisposeAll();
            };
            SetTimeout(TimeSpan.FromMinutes(3));
        }

        private void GuildConfigOnFunctionalChannelsChanged(object? sender, ChannelFunction e) {
            UpdateEmbedBuilder();
        }

        private void UpdateEmbedBuilder() {
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

            var historyChannelExists = _guildConfig.GetChannel(ChannelFunction.Log, out var logChannel);
            if (historyChannelExists && _guildConfig.IsLoggingEnabled) {
                descriptionBuilder.AppendLine(_guildConfig.LogExportType == LogExportTypes.Image
                    ? Loc.Get("Logging.OutputToImage")
                    : Loc.Get("Logging.OutputToHtml"));
            }

            descriptionBuilder.AppendLine(Loc.Get("Logging.LogChannel").Format(historyChannelExists
                ? ((ITextChannel) logChannel)!.Mention
                : Loc.Get("Logging.LogChannelMissing").Format(_guildConfig.Prefix)));
            MainBuilder.Description = descriptionBuilder.ToString();

            MainBuilder.GetOrAddField("info", s => new PriorityEmbedFieldBuilder().WithPriority(100))
                       .WithName(Loc.Get("Logging.InfoTitle")).WithValue(Loc.Get("Logging.Info").Format(_channel.Mention));

            var loggedChannels = string.Join("\n", _guildConfig.LoggedChannels.Select(arg => $"<#{arg}>"));
            MainBuilder.GetOrAddField("channelsList").WithName(Loc.Get("Logging.LoggedChannelsTitle"))
                       .WithValue(string.IsNullOrWhiteSpace(loggedChannels) ? Loc.Get("Logging.LoggedChannelsEmpty") : loggedChannels);
        }

        private LoggingChain(string? uid) : base(uid) { }
    }
}