using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bot.Config.Emoji;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Criteria;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.Music;
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
using Discord.WebSocket;
using Lavalink4NET.Player;
using SpotifyAPI.Web;

#pragma warning disable 4014

namespace Bot.DiscordRelated.Music {
    public class EmbedPlayerDisplay : PlayerDisplayBase {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private CollectorsGroup _collectorsGroup = new CollectorsGroup();
        private CommandHandlerService _commandHandlerService;
        private IUserMessage? _controlMessage;
        private SingleTask _controlMessageSendTask;
        private IDiscordClient _discordClient;
        private bool _isExternalEmojiAllowed;
        private ILocalizationProvider _loc;

        private Disposables? _playerSubscriptions;
        private IPrefixProvider _prefixProvider;

        private IMessageChannel _targetChannel;
        private IGuild? _targetGuild;
        private SingleTask _updateControlMessageTask;

        private PriorityEmbedBuilderWrapper EmbedBuilder;

        public bool NextResendForced;

        public EmbedPlayerDisplay(ITextChannel targetChannel, IDiscordClient discordClient, ILocalizationProvider loc,
                                  CommandHandlerService commandHandlerService, IPrefixProvider prefixProvider) :
            this((IMessageChannel) targetChannel, discordClient, loc, commandHandlerService, prefixProvider) {
            _targetGuild = targetChannel.Guild;
        }

        public EmbedPlayerDisplay(IMessageChannel targetChannel, IDiscordClient discordClient, ILocalizationProvider loc,
                                  CommandHandlerService commandHandlerService, IPrefixProvider prefixProvider) {
            _discordClient = discordClient;
            _loc = loc;
            _commandHandlerService = commandHandlerService;
            _prefixProvider = prefixProvider;
            _targetChannel = targetChannel;
            _updateControlMessageTask = new SingleTask(async () => {
                if (_controlMessage != null) {
                    try {
                        await _controlMessage.ModifyAsync(properties => {
                            properties.Embed = EmbedBuilder.Build();
                            properties.Content = "";
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
                        await SendControlMessageInternal();
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

            EmbedBuilder = new PriorityEmbedBuilderWrapper();
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

        public override async Task Shutdown(IEntry header, IEntry body) {
            base.Shutdown(header, body);
            _cancellationTokenSource.Cancel();
            var message = _controlMessage;
            Dispose();
            _controlMessage = null;
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
                Player.BassboostChanged.Subscribe(obj => UpdateParameters()),
                Player.VolumeChanged.Subscribe(obj => UpdateParameters()),
                Player.SocketChanged.Subscribe(obj => UpdateNode()),
                Player.StateChanged.Subscribe(obj => {
                    UpdateProgress();
                    UpdateTrackInfo();
                    UpdateControlMessage();
                }),
                Player.CurrentTrackIndexChanged.Subscribe(i => UpdateQueue())
            );
            UpdateNode();
            await ControlMessageResend();
        }

        public override void Dispose() {
            _playerSubscriptions?.Dispose();

            _cancellationTokenSource.Dispose();
            _controlMessageSendTask.Dispose();
            _updateControlMessageTask.Dispose();
            _collectorsGroup.DisposeAll();
        }

        private async Task SendControlMessageInternal() {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            UpdateParameters();
            UpdateProgress();
            UpdateQueue();
            UpdateTrackInfo();

            await CheckRestrictions();

            var oldControlMessage = _controlMessage;
            _controlMessage = await _targetChannel.SendMessageAsync(null, false, EmbedBuilder.Build());
            oldControlMessage.SafeDelete();

            _collectorsGroup.DisposeAll();
            _collectorsGroup.Add(
                CollectorsUtils.CollectReactions<string>(
                    reaction => reaction.MessageId == _controlMessage?.Id && reaction.UserId != Program.Client.CurrentUser.Id,
                    async (args, s) => {
                        args.RemoveReason();
                        await _commandHandlerService.ExecuteCommand(s, new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                    },
                    (CommonEmoji.LegacyTrackPrevious, () => Player.TrackPosition.TotalSeconds > 15 ? "seek 0s" : "skip -1"),
                    (CommonEmoji.LegacyPlay, () => "resume"),
                    (CommonEmoji.LegacyPause, () => "pause"),
                    (CommonEmoji.LegacyTrackNext, () => "skip"),
                    (CommonEmoji.LegacyStop, () => "stop"),
                    (CommonEmoji.LegacyRepeat, () => "repeat"),
                    (CommonEmoji.LegacyShuffle, () => "shuffle")
                ));
            var addReactionsAsync = _controlMessage.AddReactionsAsync(new IEmote[] {
                CommonEmoji.LegacyTrackPrevious, CommonEmoji.LegacyPlay, CommonEmoji.LegacyPause, CommonEmoji.LegacyTrackNext,
                CommonEmoji.LegacyStop, CommonEmoji.LegacyRepeat, CommonEmoji.LegacyShuffle
            }, new RequestOptions {CancelToken = _cancellationTokenSource.Token});

            _collectorsGroup.Controllers.Add(CollectorsUtils.CollectMessage(_controlMessage.Channel, message => true, async args => {
                args.StopCollect();
                try {
                    await addReactionsAsync;
                }
                catch (Exception) {
                    // ignored
                }

                _controlMessage?.AddReactionAsync(CommonEmoji.LegacyArrowDown, new RequestOptions {CancelToken = _cancellationTokenSource.Token});
                _collectorsGroup.Controllers.Add(CollectorsUtils.CollectReaction(_controlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyArrowDown), async emoteCollectorEventArgs => {
                        emoteCollectorEventArgs.RemoveReason();
                        if ((await _controlMessage.Channel.GetMessagesAsync(1).FlattenAsync()).FirstOrDefault()?.Id == _controlMessage.Id) {
                            return;
                        }

                        NextResendForced = true;
                        await _controlMessageSendTask.Execute(false, TimeSpan.Zero);
                    }, CollectorFilter.IgnoreSelf));
            }));
        }

        private async Task CheckRestrictions() {
            if (_targetGuild != null) {
                var guildUser = (await _targetGuild.GetUserAsync(Program.Client.CurrentUser.Id)).GetPermissions((IGuildChannel) _targetChannel);
                _isExternalEmojiAllowed = guildUser.UseExternalEmojis;
                var text = "";
                if (!guildUser.ManageMessages) text += _loc.Get("Music.WarningEmojiRemoval") + "\n";
                if (!guildUser.AddReactions) text += _loc.Get("Music.WarningEmojiAdding") + "\n";
                if (!guildUser.UseExternalEmojis) text += _loc.Get("Music.WarningCustomEmoji") + "\n";

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
                    LoopingState.One => _isExternalEmojiAllowed ? CommonEmojiStrings.Instance.RepeatOnce : "🔂",
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
            var volume = Player.Volume * 200;
            var volumeText = volume < 50 || volume > 150 ? $"🔉 ***{volume}%***\n" : $"🔉 {volume}%\n";
            EmbedBuilder.Fields["Parameters"].Value = volumeText + $"🅱️ {Player.BassBoostMode}";
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
                    var author = track.GetRequester();
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
            EmbedBuilder.WithFooter($"Powered by {Program.Client.CurrentUser.Username} | {(Player.LavalinkSocket as EnlivenLavalinkClusterNode)?.Label}");
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