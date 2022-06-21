using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Criteria;
using Bot.DiscordRelated.MessageComponents;
using Bot.Music.Spotify;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Config.Emoji;
using Common.Criteria;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Music;
using Common.Music.Controller;
using Common.Music.Players;
using Common.Music.Tracks;
using Common.Utils;
using Discord;
using Discord.Net;
using Lavalink4NET.Player;
using Newtonsoft.Json;
using NLog;
using Tyrrrz.Extensions;

#pragma warning disable 4014

namespace Bot.DiscordRelated.Music {
    public class EmbedPlayerDisplay : PlayerDisplayBase {
        private readonly CommandHandlerService _commandHandlerService;
        private readonly ILocalizationProvider _loc;
        private readonly IPrefixProvider _prefixProvider;
        private readonly IGuild? _targetGuild;
        private readonly SingleTask _updateControlMessageTask;
        private readonly SingleTask _controlMessageSendTask;
        private readonly MessageComponentService _messageComponentService;
        private readonly IDiscordClient _discordClient;
        private readonly ILogger _logger;

        private readonly EnlivenEmbedBuilder _embedBuilder;
        private CancellationTokenSource _cancellationTokenSource = new();
        private IUserMessage? _controlMessage;
        private bool _isExternalEmojiAllowed;
        private Disposables? _playerSubscriptions;
        private IMessageChannel _targetChannel;
        public bool NextResendForced;
        private MessageComponent? _messageComponent;
        private EnlivenComponentBuilder? _messageComponentManager;

        public EmbedPlayerDisplay(ITextChannel targetChannel, IDiscordClient discordClient, ILocalizationProvider loc,
                                  CommandHandlerService commandHandlerService, IPrefixProvider prefixProvider, MessageComponentService messageComponentService, 
                                  ILogger logger) :
            this((IMessageChannel)targetChannel, discordClient, loc, commandHandlerService, prefixProvider, messageComponentService, logger) {
            _targetGuild = targetChannel.Guild;
        }

        public EmbedPlayerDisplay(IMessageChannel targetChannel, IDiscordClient discordClient, ILocalizationProvider loc,
                                  CommandHandlerService commandHandlerService, IPrefixProvider prefixProvider, MessageComponentService messageComponentService, 
                                  ILogger logger) {
            _messageComponentService = messageComponentService;
            _logger = logger;
            _loc = loc;
            _commandHandlerService = commandHandlerService;
            _prefixProvider = prefixProvider;
            _targetChannel = targetChannel;
            _discordClient = discordClient;

            _embedBuilder = new EnlivenEmbedBuilder();
            _embedBuilder.AddField("State", loc.Get("Music.Empty"), loc.Get("Music.Empty"), true);
            _embedBuilder.AddField("Parameters", loc.Get("Music.Parameters"), loc.Get("Music.Empty"), true);
            _embedBuilder.AddField("Effects", loc.Get("Music.Effects"), loc.Get("Music.Empty"), isEnabled: false);
            _embedBuilder.AddField("Queue", loc.Get("Music.Queue").Format(0, 0, 0), loc.Get("Music.Empty"));
            _embedBuilder.AddField("RequestHistory", loc.Get("Music.RequestHistory"), loc.Get("Music.Empty"));
            _embedBuilder.AddField("Warnings", loc.Get("Music.Warning"), loc.Get("Music.Empty"), false, 100, false);

            _controlMessageSendTask = new SingleTask(SendControlMessageInternal) { BetweenExecutionsDelay = TimeSpan.FromSeconds(30), CanBeDirty = false };
            _updateControlMessageTask = new SingleTask(UpdateControlMessageInternal) { BetweenExecutionsDelay = TimeSpan.FromSeconds(1.5), CanBeDirty = true };
        }

        private async Task SendControlMessageInternal(SingleTaskExecutionData data) {
            try {
                var shouldResend =
                    NextResendForced
                 || _controlMessage == null
                 || await EnsureMessage.NotExists(_targetChannel, _controlMessage.Id, 3);
                if (shouldResend) {
                    NextResendForced = false;
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource = new CancellationTokenSource();

                    UpdateParameters();
                    UpdateProgress();
                    UpdateQueue();
                    UpdateTrackInfo();

                    if (_targetGuild != null) {
                        var guildUser = await _targetGuild.GetUserAsync(_discordClient.CurrentUser.Id);
                        var channelPerms = guildUser.GetPermissions((IGuildChannel)_targetChannel);
                        _isExternalEmojiAllowed = channelPerms.UseExternalEmojis;
                        // ReSharper disable once AssignmentInConditionalExpression
                        if (_embedBuilder.Fields["Warnings"].IsEnabled = !channelPerms.UseExternalEmojis) {
                            _embedBuilder.Fields["Warnings"].Value = _loc.Get("Music.WarningCustomEmoji");
                        }
                    }

                    var oldControlMessage = _controlMessage;
                    await SendControlMessage_WithMessageComponents();
                    _logger.Debug("Sent player embed controll message. Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}", _targetGuild?.Id, _targetChannel.Id, _controlMessage.Id);
                    oldControlMessage.SafeDelete();
                }
                else {
                    data.OverrideDelay = TimeSpan.FromSeconds(5);
                }
            }
            catch (Exception) {
                // ignored
            }
        }

        private async Task UpdateControlMessageInternal(SingleTaskExecutionData data) {
            if (_controlMessage != null) {
                try {
                    _logger.Trace("Modifying embed control message. Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}", _targetGuild?.Id, _targetChannel.Id, _controlMessage.Id);
                    await _controlMessage.ModifyAsync(properties => {
                        properties.Embed = _embedBuilder!.Build();
                        properties.Content = "";
                        properties.Components = _messageComponent;
                    }, new RequestOptions {
                        CancelToken = _cancellationTokenSource.Token,
                        RatelimitCallback = RatelimitCallback,
                        RetryMode = RetryMode.AlwaysFail
                    });
                }
                catch (RateLimitedException) {
                    _logger.Debug("Got rate limited exception while updating player embed control message. Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}", _targetGuild?.Id, _targetChannel.Id, _controlMessage.Id);
                }
                catch (Exception e) {
                    _logger.Debug(e, "Failed to update embed control message. Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}", _targetGuild?.Id, _targetChannel.Id, _controlMessage.Id);
                    if (_controlMessage != null) {
                        (await _targetChannel.GetMessageAsync(_controlMessage.Id)).SafeDelete();
                        _controlMessage = null;
                    }

                    ControlMessageResend(_targetChannel);
                }
            }

            Task RatelimitCallback(IRateLimitInfo info) {
                _logger.Log(LogLevel.Trace, "Recieved ratelimit info for updating player embed control message. Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}.\nRatelimit info: {ratelimit info}", 
                    _targetGuild?.Id, _targetChannel.Id, _controlMessage.Id, JsonConvert.SerializeObject(info));
                if (info.Remaining <= 1) {
                    data.OverrideDelay = info.ResetAfter > data.BetweenExecutionsDelay ? info.ResetAfter : null;
                    _logger.Debug("Ratelimit exceed for updating player embed control message. Waiting {ResetAfter}. Guild: {TargetGuildId}. Channel: {TargetChannelId}. Message id: {ControlMessageId}",
                        info.ResetAfter, _targetGuild?.Id, _targetChannel.Id, _controlMessage.Id);
                }
                return Task.CompletedTask;
            }
        }

        public override async Task LeaveNotification(IEntry header, IEntry body) {
            try {
                var embedBuilder = new EmbedBuilder().WithColor(Color.Gold).WithTitle(header.Get(_loc)).WithDescription(body.Get(_loc));
                await _targetChannel.SendMessageAsync(null, false, embedBuilder.Build());
            }
            catch (Exception) {
                // ignored
            }
        }

        public override async Task ExecuteShutdown(IEntry header, IEntry body) {
            base.ExecuteShutdown(header, body);
            _cancellationTokenSource.Cancel();
            var message = _controlMessage;
            _controlMessage = null;

            _playerSubscriptions?.Dispose();
            _cancellationTokenSource.Dispose();
            _controlMessageSendTask.Dispose();
            _updateControlMessageTask.Dispose();

            if (message != null) {
                var embed = new EmbedBuilder()
                    .WithColor(Color.Gold)
                    .WithTitle(header.Get(_loc))
                    .WithDescription(body.Get(_loc))
                    .Build();
                var components = new ComponentBuilder()
                    .WithButton(_loc.Get("Music.RestoreStoppedPlayerButton"), "restoreStoppedPlayer")
                    .Build();
                await message.ModifyAsync(properties => {
                    properties.Content = Optional<string>.Unspecified;
                    properties.Embed = embed;
                    properties.Components = components;
                });
            }
            else {
                await LeaveNotification(header, body);
            }
        }

        public override async Task ChangePlayer(FinalLavalinkPlayer newPlayer) {
            _playerSubscriptions?.Dispose();
            await base.ChangePlayer(newPlayer);
            _playerSubscriptions = new Disposables(
                Player.QueueHistory.HistoryChanged.Subscribe(collection => {
                    _embedBuilder.Fields["RequestHistory"].Value = collection.GetLastHistory(_loc, out var isChanged).IsBlank(_loc.Get("Music.Empty"));
                    if (isChanged) UpdateControlMessage();
                }),
                Player.Playlist.Changed.Subscribe(playlist => UpdateQueue()),
                Player.VolumeChanged.Subscribe(obj => UpdateParameters()),
                Player.SocketChanged.Subscribe(obj => UpdateNode()),
                Player.StateChanged.Subscribe(obj => {
                    UpdateProgress();
                    UpdateTrackInfo();
                    UpdateControlMessage();
                    UpdateMessageComponents();
                }),
                Player.FiltersChanged.Subscribe(_ => UpdateEffects()),
                Player.CurrentTrackIndexChanged.Subscribe(i => UpdateQueue())
            );
            UpdateNode();
            await ControlMessageResend();
        }

        private async Task SendControlMessage_WithMessageComponents() {
            _messageComponentManager?.Dispose();
            _messageComponentManager = _messageComponentService.GetBuilder();
            var btnTemplate = new EnlivenButtonBuilder().WithTargetRow(0).WithStyle(ButtonStyle.Secondary);
            _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyTrackPrevious).WithCustomId("TrackPrevious"));
            _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyFastReverse).WithCustomId("FastReverse"));
            _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyPlayPause).WithCustomId("PlayPause").WithStyle(ButtonStyle.Primary));
            _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyFastForward).WithCustomId("FastForward"));
            _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyTrackNext).WithCustomId("TrackNext"));
            btnTemplate.WithTargetRow(1);
            _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.BookmarkTabs).WithCustomId("Queue"));
            _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyShuffle).WithCustomId("Shuffle"));
            _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyStop).WithCustomId("Stop").WithStyle(ButtonStyle.Danger));
            _messageComponentManager.WithButton(btnTemplate.Clone().WithCustomId("Repeat"));
            _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.E).WithCustomId("Effects"));
            _messageComponentManager.SetCallback(async (s, component, arg3) => {
                var command = s switch {
                    "TrackPrevious" => Player.TrackPosition.TotalSeconds > 15 ? "seek 0s" : "skip -1",
                    "FastReverse"   => "rw 20s",
                    "PlayPause"     => Player.State == PlayerState.Paused ? "resume" : "pause",
                    "FastForward"   => "ff 20s",
                    "TrackNext"     => "skip",
                    "Queue"         => "queue",
                    "Shuffle"       => "shuffle",
                    "Stop"          => "stop",
                    "Repeat"        => "repeat",
                    "Effects"       => "effects",
                    _               => throw new ArgumentOutOfRangeException()
                };

                if (!string.IsNullOrEmpty(command)) {
                    await _commandHandlerService.ExecuteCommand(command, new ComponentCommandContext(_discordClient, component),
                        component.User.Id.ToString());
                }
            });

            UpdateMessageComponents();

            _messageComponent = _messageComponentManager.Build();
            _controlMessage = await _targetChannel.SendMessageAsync(null, false, _embedBuilder.Build(), components: _messageComponent);
            _messageComponentManager.AssociateWithMessage(_controlMessage);
        }

        public void UpdateMessageComponents() {
            if (_messageComponentManager == null) return;
            var entries = _messageComponentManager.Entries;
            var updated = false;

            var targetPlayPauseEmoji = Player.State == PlayerState.Paused ? CommonEmoji.LegacyPlay : CommonEmoji.LegacyPause;
            updated = updated || Equals(entries["PlayPause"].Emote, targetPlayPauseEmoji);
            entries["PlayPause"].Emote = targetPlayPauseEmoji;

            var nextRepeatState = Player.LoopingState.Next() switch {
                LoopingState.One => CommonEmoji.RepeatOneBox,
                LoopingState.All => CommonEmoji.RepeatBox,
                LoopingState.Off => CommonEmoji.RepeatOffBox,
                _                => throw new ArgumentOutOfRangeException()
            };
            updated = updated || Equals(entries["Repeat"].Emote, nextRepeatState);
            entries["Repeat"].Emote = nextRepeatState;

            if (updated) _messageComponent = _messageComponentManager.Build();
        }

        public void UpdateProgress(bool background = false) {
            if (Player.CurrentTrack != null) {
                _embedBuilder.Fields["State"].Name = _loc.Get("Music.RequestedBy").Format(Player.CurrentTrack.GetRequester());

                var progressPercentage = Convert.ToInt32(Player.Position.Position.TotalSeconds / Player.CurrentTrack.Duration.TotalSeconds * 100);
                var progressBar = (_isExternalEmojiAllowed ? ProgressEmoji.CustomEmojiPack : ProgressEmoji.TextEmojiPack).GetProgress(progressPercentage);

                var stateString = Player.State switch {
                    PlayerState.Playing => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.Play : "▶",
                    PlayerState.Paused  => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.Pause : "⏸",
                    _                   => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.Stop : "⏹"
                };
                var loopingStateString = Player.LoopingState switch {
                    LoopingState.One => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.RepeatOne : "🔂",
                    LoopingState.All => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.Repeat : "🔁",
                    LoopingState.Off => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.RepeatOff : "❌",
                    _                => throw new InvalidEnumArgumentException()
                };
                var spotifyId = (Player.CurrentTrack is SpotifyLavalinkTrack spotifyLavalinkTrack)
                    ? spotifyLavalinkTrack.RelatedSpotifyTrackWrapper.Id
                    : null;
                var spotifyEmojiExists = spotifyId != null && _isExternalEmojiAllowed;
                var sb = new StringBuilder(Player.Position.Position.FormattedToString());
                if (Player.CurrentTrack.IsSeekable) {
                    sb.Append(" / ");
                    sb.Append(Player.CurrentTrack.Duration.FormattedToString());
                }

                var space = new string(' ', Math.Max(0, ((spotifyEmojiExists ? 18 : 22) - sb.Length) / 2));
                var detailsBar = stateString + '`' + space + sb + space + '`' + loopingStateString;
                if (spotifyEmojiExists) detailsBar += $"[{CommonEmojiStrings.Instance.Spotify}](https://open.spotify.com/track/{spotifyId})";
                _embedBuilder.Fields["State"].Value = progressBar + "\n" + detailsBar;
            }
            else {
                _embedBuilder.Fields["State"].Name = _loc.Get("Music.Playback");
                _embedBuilder.Fields["State"].Value = _loc.Get("Music.PlaybackNothingPlaying");
            }
        }

        private void UpdateTrackInfo() {
            if (Player.CurrentTrackIndex >= Player.Playlist.Count && Player.Playlist.Count != 0) {
                _embedBuilder.Author = new EmbedAuthorBuilder();
                _embedBuilder.Title = _loc.Get("Music.QueueEnd");
                _embedBuilder.Url = "";
            }
            else if ((Player.State != PlayerState.NotPlaying || Player.State != PlayerState.NotConnected || Player.State != PlayerState.Destroyed) &&
                     Player.CurrentTrack != null) {
                var iconUrl = Player.CurrentTrack.Provider == StreamProvider.YouTube
                    ? $"https://img.youtube.com/vi/{Player.CurrentTrack?.TrackIdentifier}/0.jpg"
                    : null;
                _embedBuilder
                    .WithAuthor(Player.CurrentTrack!.Author.SafeSubstring(Constants.MaxEmbedAuthorLength, "...").IsBlank("Unknown"), iconUrl)
                    .WithTitle(MusicController.EscapeTrack(Player.CurrentTrack!.Title).SafeSubstring(Discord.EmbedBuilder.MaxTitleLength, "...")!)
                    .WithUrl(Player.CurrentTrack.Source!);
            }
            else {
                _embedBuilder.Author = new EmbedAuthorBuilder();
                _embedBuilder.Title = _loc.Get("Music.Waiting");
                _embedBuilder.Url = "";
            }
        }

        private void UpdateEffects() {
            var effectsText = ProcessEffectsText(Player.Effects);
            _embedBuilder.Fields["Effects"].Value = effectsText.Or("Placeholder");
            _embedBuilder.Fields["Effects"].IsEnabled = !effectsText.IsNullOrWhiteSpace();

            string ProcessEffectsText(ImmutableList<PlayerEffectUse> effects) {
                try {
                    var text = effects.Select(use => use.Effect.DisplayName).JoinToString(" > ");
                    if (text.Length < 20) {
                        _effectsInParameters = "\n" + text;
                        return "";
                    }
                    _effectsInParameters = null;
                    return effects.Select(use => $"`{use.Effect.DisplayName}`").JoinToString(" > ");
                }
                finally {
                    UpdateParameters();
                }
            }
        }

        private string? _effectsInParameters;
        private void UpdateParameters() {
            var volume = (int)(Player.Volume * 200);
            var volumeText = volume < 50 || volume > 150 ? $"🔉 ***{volume}%***\n" : $"🔉 {volume}%";
            _embedBuilder.Fields["Parameters"].Value = volumeText + _effectsInParameters;
        }

        private void UpdateQueue() {
            if (Player.Playlist.Count == 0) {
                _embedBuilder.Fields["Queue"].Name = _loc.Get("Music.QueueEmptyTitle");
                _embedBuilder.Fields["Queue"].Value = _loc.Get("Music.QueueEmpty", _prefixProvider.GetPrefix());
            }
            else {
                _embedBuilder.Fields["Queue"].Name =
                    _loc.Get("Music.Queue").Format(Player.CurrentTrackIndex + 1, Player.Playlist.Count,
                        Player.Playlist.TotalPlaylistLength.FormattedToString());
                _embedBuilder.Fields["Queue"].Value = $"```py\n{GetPlaylistString()}```";
            }

            StringBuilder GetPlaylistString() {
                var globalStringBuilder = new StringBuilder();
                string? lastAuthor = null;
                var authorStringBuilder = new StringBuilder();
                for (var i = Math.Max(Player.CurrentTrackIndex - 1, 0); i < Player.CurrentTrackIndex + 5; i++) {
                    if (!Player.Playlist.TryGetValue(i, out var track)) continue;
                    var author = track!.GetRequester();
                    if (author != lastAuthor && lastAuthor != null) FinalizeBlock();
                    authorStringBuilder.Replace("└", "├").Replace("▬", "│");
                    authorStringBuilder.Append(GetTrackString(MusicController.EscapeTrack(track!.Title),
                        i + 1, Player.CurrentTrackIndex == i));
                    lastAuthor = author;
                }

                FinalizeBlock();

                void FinalizeBlock() {
                    globalStringBuilder.AppendLine($"─────┬────{lastAuthor}");
                    globalStringBuilder.Append(authorStringBuilder.Replace("▬", " "));

                    authorStringBuilder.Clear();
                }

                StringBuilder GetTrackString(string title, int trackNumber, bool isCurrent) {
                    var sb = new StringBuilder();
                    sb.AppendLine($"{(isCurrent ? "@" : " ")}{trackNumber}    ".SafeSubstring(0, 5) + "└" + title);

                    return sb;
                }

                return globalStringBuilder;
            }
        }

        private Task UpdateNode() {
            _embedBuilder.WithFooter($"Powered by {_discordClient.CurrentUser.Username} | {(Player.LavalinkSocket as EnlivenLavalinkClusterNode)?.Label}");
            return Task.CompletedTask;
        }

        public Task UpdateControlMessage(bool background = false) {
            return _updateControlMessageTask.IsDisposed ? Task.CompletedTask : _updateControlMessageTask.Execute(!background);
        }

        public async Task ControlMessageResend(IMessageChannel? channel = null) {
            if (_controlMessageSendTask.IsDisposed) return;

            _targetChannel = channel ?? _targetChannel;
            await _controlMessageSendTask.Execute(false);
        }
    }
}