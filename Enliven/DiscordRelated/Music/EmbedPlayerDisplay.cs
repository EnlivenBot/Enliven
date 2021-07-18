using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bot.Config.Emoji;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Criteria;
using Bot.DiscordRelated.MessageComponents;
using Bot.Music.Spotify;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Common.Criteria;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Music;
using Common.Music.Controller;
using Common.Music.Players;
using Common.Music.Tracks;
using Common.Utils;
using Discord;
using Lavalink4NET.Player;

#pragma warning disable 4014

namespace Bot.DiscordRelated.Music {
    public class EmbedPlayerDisplay : PlayerDisplayBase {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private CollectorsGroup _collectorsGroup = new CollectorsGroup();
        private CommandHandlerService _commandHandlerService;
        private IUserMessage? _controlMessage;
        private bool _isExternalEmojiAllowed;
        private ILocalizationProvider _loc;

        private Disposables? _playerSubscriptions;
        private IPrefixProvider _prefixProvider;

        private IMessageChannel _targetChannel;
        private IGuild? _targetGuild;
        private SingleTask _updateControlMessageTask;
        private SingleTask _controlMessageSendTask;

        private EnlivenEmbedBuilder EmbedBuilder;

        public bool NextResendForced;
        private MessageComponentService _messageComponentService;
        private MessageComponent? _messageComponent;
        private EnlivenComponentBuilder? _messageComponentManager;

        public EmbedPlayerDisplay(ITextChannel targetChannel, IDiscordClient discordClient, ILocalizationProvider loc,
                                  CommandHandlerService commandHandlerService, IPrefixProvider prefixProvider, MessageComponentService messageComponentService) :
            this((IMessageChannel) targetChannel, discordClient, loc, commandHandlerService, prefixProvider, messageComponentService) {
            _targetGuild = targetChannel.Guild;
        }

        public EmbedPlayerDisplay(IMessageChannel targetChannel, IDiscordClient discordClient, ILocalizationProvider loc,
                                  CommandHandlerService commandHandlerService, IPrefixProvider prefixProvider, MessageComponentService messageComponentService) {
            _messageComponentService = messageComponentService;
            _loc = loc;
            _commandHandlerService = commandHandlerService;
            _prefixProvider = prefixProvider;
            _targetChannel = targetChannel;
            _updateControlMessageTask = new SingleTask(async () => {
                if (_controlMessage != null) {
                    try {
                        await _controlMessage.ModifyAsync(properties => {
                            properties.Embed = EmbedBuilder!.Build();
                            properties.Content = "";
                            properties.Components = _messageComponent;
                        }, new RequestOptions {CancelToken = _cancellationTokenSource.Token});
                    }
                    catch (Exception) {
                        if (_controlMessage != null) {
                            (await _targetChannel.GetMessageAsync(_controlMessage.Id)).SafeDelete();
                            _controlMessage = null;
                        }

                        ControlMessageResend(_targetChannel);
                    }
                }
            }) {BetweenExecutionsDelay = TimeSpan.FromSeconds(1.5), CanBeDirty = true};
            _controlMessageSendTask = new SingleTask(async data => {
                try {
                    if (NextResendForced || await new EnsureLastMessage(_targetChannel, _controlMessage?.Id ?? 0, 3) {IsNullableTrue = true}.Invert().JudgeAsync()) {
                        NextResendForced = false;
                        await SendControlMessage();
                    }
                    else {
                        data.OverrideDelay = TimeSpan.FromSeconds(5);
                    }
                }
                catch (Exception) {
                    // ignored
                }
            }) {
                BetweenExecutionsDelay = TimeSpan.FromSeconds(30), CanBeDirty = false, IsDelayResetByExecute = false,
            };

            EmbedBuilder = new EnlivenEmbedBuilder();
            EmbedBuilder.AddField("State", loc.Get("Music.Empty"), loc.Get("Music.Empty"), true);
            EmbedBuilder.AddField("Parameters", loc.Get("Music.Parameters"), loc.Get("Music.Empty"), true);
            EmbedBuilder.AddField("Queue", loc.Get("Music.Queue").Format(0, 0, 0), loc.Get("Music.Empty"));
            EmbedBuilder.AddField("RequestHistory", loc.Get("Music.RequestHistory"), loc.Get("Music.Empty"));
            EmbedBuilder.AddField("Warnings", loc.Get("Music.Warning"), loc.Get("Music.Empty"), false, 100, false);
        }

        public override async Task LeaveNotification(IEntry header, IEntry body) {
            try {
                await _targetChannel.SendMessageAsync(null, false,
                    new EmbedBuilder().WithColor(Color.Gold).WithTitle(header.Get(_loc)).WithDescription(body.Get(_loc)).Build());
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
            _collectorsGroup.DisposeAll();
            
            if (message != null) {
                try {
                    message.RemoveAllReactionsAsync();
                }
                catch (Exception) {
                    // ignored
                }

                await message.ModifyAsync(properties => {
                    properties.Content = Optional<string>.Unspecified;
                    properties.Embed = new EmbedBuilder().WithColor(Color.Gold).WithTitle(header.Get(_loc)).WithDescription(body.Get(_loc)).Build();
                    properties.Components = new ComponentBuilder().Build();
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
                    EmbedBuilder.Fields["RequestHistory"].Value = collection.GetLastHistory(_loc, out var isChanged).IsBlank(_loc.Get("Music.Empty"));
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
                Player.CurrentTrackIndexChanged.Subscribe(i => UpdateQueue())
            );
            UpdateNode();
            await ControlMessageResend();
        }
        
        private async Task SendControlMessage() {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            UpdateParameters();
            UpdateProgress();
            UpdateQueue();
            UpdateTrackInfo();

            await CheckRestrictions();

            var oldControlMessage = _controlMessage;
            await SendControlMessage_WithMessageComponents();
            oldControlMessage.SafeDelete();
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
            _messageComponentManager.WithButton(btnTemplate.Clone().WithEmote(CommonEmoji.LegacyArrowDown).WithCustomId("Down").WithDisabled(true));
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
                    "Down"          => await TryPerformArrowDownResendWithComponents(),
                    _               => throw new ArgumentOutOfRangeException()
                };

                if (!string.IsNullOrEmpty(command)) {
                    await _commandHandlerService.ExecuteCommand(command, new ComponentCommandContext(EnlivenBot.Client, component),
                        component.User.Id.ToString());
                }

                async Task<string?> TryPerformArrowDownResendWithComponents() {
                    if ((await _controlMessage!.Channel.GetMessagesAsync(1).FlattenAsync()).FirstOrDefault()?.Id == _controlMessage.Id) {
                        return null;
                    }
                    NextResendForced = true;
                    await _controlMessageSendTask.Execute(false, TimeSpan.Zero);
                    _messageComponentManager.GetButton("Down")!.WithDisabled(true);
                    return null;
                }
            });

            UpdateMessageComponents();

            _messageComponent = _messageComponentManager.Build();
            _controlMessage = await _targetChannel.SendMessageAsync(null, false, EmbedBuilder.Build(), component: _messageComponent);
            _messageComponentManager.AssociateWithMessage(_controlMessage);
            
            _collectorsGroup.Controllers.Add(CollectorsUtils.CollectMessage(_controlMessage.Channel, message => true, async args => {
                args.StopCollect();
                _messageComponentManager.GetButton("Down")!.WithDisabled(false);
            }));
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

        private async Task CheckRestrictions() {
            if (_targetGuild != null) {
                var guildUser = await _targetGuild.GetUserAsync(EnlivenBot.Client.CurrentUser.Id);
                var channelPerms = guildUser.GetPermissions((IGuildChannel) _targetChannel);
                _isExternalEmojiAllowed = channelPerms.UseExternalEmojis;
                var text = "";
                if (!channelPerms.UseExternalEmojis) text += _loc.Get("Music.WarningCustomEmoji") + "\n";

                // ReSharper disable once AssignmentInConditionalExpression
                if (EmbedBuilder.Fields["Warnings"].IsEnabled = !string.IsNullOrWhiteSpace(text)) {
                    EmbedBuilder.Fields["Warnings"].Value = text;
                }
            }
        }

        public void UpdateProgress(bool background = false) {
            if (Player.CurrentTrack != null) {
                EmbedBuilder.Fields["State"].Name = _loc.Get("Music.RequestedBy").Format(Player.CurrentTrack.GetRequester());

                var progressPercentage = Convert.ToInt32(Player.TrackPosition.TotalSeconds / Player.CurrentTrack.Duration.TotalSeconds * 100);
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
                var sb = new StringBuilder(Player.TrackPosition.FormattedToString());
                if (Player.CurrentTrack.IsSeekable) {
                    sb.Append(" / ");
                    sb.Append(Player.CurrentTrack.Duration.FormattedToString());
                }

                var space = new string(' ', Math.Max(0, ((spotifyEmojiExists ? 18 : 22) - sb.Length) / 2));
                var detailsBar = stateString + '`' + space + sb + space + '`' + loopingStateString;
                if (spotifyEmojiExists) detailsBar += $"[{CommonEmojiStrings.Instance.Spotify}](https://open.spotify.com/track/{spotifyId})";
                EmbedBuilder.Fields["State"].Value = progressBar + "\n" + detailsBar;
            }
            else {
                EmbedBuilder.Fields["State"].Name = _loc.Get("Music.Playback");
                EmbedBuilder.Fields["State"].Value = _loc.Get("Music.PlaybackNothingPlaying");
            }
        }

        private void UpdateTrackInfo() {
            if (Player.CurrentTrackIndex >= Player.Playlist.Count && Player.Playlist.Count != 0) {
                EmbedBuilder.Author = new EmbedAuthorBuilder();
                EmbedBuilder.Title = _loc.Get("Music.QueueEnd");
                EmbedBuilder.Url = "";
            }
            else if ((Player.State != PlayerState.NotPlaying || Player.State != PlayerState.NotConnected || Player.State != PlayerState.Destroyed) &&
                     Player.CurrentTrack != null) {
                var iconUrl = Player.CurrentTrack.Provider == StreamProvider.YouTube
                    ? $"https://img.youtube.com/vi/{Player.CurrentTrack?.TrackIdentifier}/0.jpg"
                    : null;
                EmbedBuilder
                    .WithAuthor(Player.CurrentTrack!.Author.SafeSubstring(Constants.MaxEmbedAuthorLength, "...").IsBlank("Unknown"), iconUrl)
                    .WithTitle(MusicController.EscapeTrack(Player.CurrentTrack!.Title).SafeSubstring(Discord.EmbedBuilder.MaxTitleLength, "...")!)
                    .WithUrl(Player.CurrentTrack.Source!);
            }
            else {
                EmbedBuilder.Author = new EmbedAuthorBuilder();
                EmbedBuilder.Title = _loc.Get("Music.Waiting");
                EmbedBuilder.Url = "";
            }
        }

        private void UpdateParameters() {
            var volume = (int)Player.Volume * 200;
            var volumeText = volume < 50 || volume > 150 ? $"🔉 ***{volume}%***\n" : $"🔉 {volume}%\n";
            EmbedBuilder.Fields["Parameters"].Value = volumeText;
        }

        private void UpdateQueue() {
            if (Player.Playlist.Count == 0) {
                EmbedBuilder.Fields["Queue"].Name = _loc.Get("Music.QueueEmptyTitle");
                EmbedBuilder.Fields["Queue"].Value = _loc.Get("Music.QueueEmpty", _prefixProvider.GetPrefix());
            }
            else {
                EmbedBuilder.Fields["Queue"].Name =
                    _loc.Get("Music.Queue").Format(Player.CurrentTrackIndex + 1, Player.Playlist.Count,
                        Player.Playlist.TotalPlaylistLength.FormattedToString());
                EmbedBuilder.Fields["Queue"].Value = $"```py\n{GetPlaylistString()}```";
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
            EmbedBuilder.WithFooter($"Powered by {EnlivenBot.Client.CurrentUser.Username} | {(Player.LavalinkSocket as EnlivenLavalinkClusterNode)?.Label}");
            return Task.CompletedTask;
        }

        public Task UpdateControlMessage(bool background = false) {
            return _updateControlMessageTask.IsDisposed ? Task.CompletedTask : _updateControlMessageTask.Execute(!background);
        }

        public async Task SetChannel(IMessageChannel channel) {
            if (channel.Id != _targetChannel.Id) {
                await ControlMessageResend(channel);
            }
        }

        public async Task ControlMessageResend(IMessageChannel? channel = null) {
            if (_controlMessageSendTask.IsDisposed) return;

            if (channel != null) CheckRestrictions();
            _targetChannel = channel ?? _targetChannel;
            await _controlMessageSendTask.Execute(false);
        }
    }
}