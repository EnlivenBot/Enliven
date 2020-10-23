using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Config.Emoji;
using Bot.Config.Localization.Entries;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Criteria;
using Bot.DiscordRelated.Music.Tracks;
using Bot.Music;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.History;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Decoding;
using Lavalink4NET.Events;
using Lavalink4NET.Player;

#pragma warning disable 1998

#pragma warning disable 4014

namespace Bot.DiscordRelated.Music {
    public sealed class EmbedPlaybackPlayer : PlaylistLavalinkPlayer {
        private readonly HistoryCollection _queueHistory = new HistoryCollection(Constants.MaxQueueHistoryChars, 1000, false);
        private PriorityEmbedBuilderWrapper EmbedBuilder = new PriorityEmbedBuilderWrapper();
        public bool UpdatePlayback;

        // ReSharper disable once UnusedParameter.Local
        public EmbedPlaybackPlayer(ulong guildId) : base(guildId) {
            _updateControlMessageTask = new SingleTask(async () => {
                if (ControlMessage != null) {
                    try {
                        await ControlMessage.ModifyAsync(properties => {
                            properties.Embed = EmbedBuilder.Build();
                            properties.Content = "";
                        });
                    }
                    catch (Exception) {
                        (await _controlMessageChannel.GetMessageAsync(ControlMessage.Id)).SafeDelete();
                        ControlMessage = null;
                        EnqueueControlMessageSend(_controlMessageChannel);
                    }
                }
            }) {BetweenExecutionsDelay = TimeSpan.FromSeconds(1.5), CanBeDirty = true};
            _controlMessageSendTask = new SingleTask(async () => {
                try {
                    await SetControlMessage(await _controlMessageChannel.SendMessageAsync(null, false, EmbedBuilder.Build()));
                }
                catch (Exception) {
                    // ignored
                }
            }) {
                BetweenExecutionsDelay = TimeSpan.FromSeconds(2), CanBeDirty = true, IsDelayResetByExecute = true,
                NeedDirtyExecuteCriterion = new EnsureLastMessage(_controlMessageChannel, ControlMessage?.Id ?? 0) {IsNullableTrue = true}.Invert()
            };
            EmbedBuilder.AddField("State", Loc.Get("Music.Empty"), Loc.Get("Music.Empty"), true);
            EmbedBuilder.AddField("Parameters", Loc.Get("Music.Parameters"), Loc.Get("Music.Empty"), true);
            EmbedBuilder.AddField("Queue", Loc.Get("Music.Queue").Format(0, 0, 0), Loc.Get("Music.Empty"));
            EmbedBuilder.AddField("RequestHistory", Loc.Get("Music.RequestHistory"), Loc.Get("Music.Empty"));
            EmbedBuilder.AddField("Warnings", Loc.Get("Music.Warning"), Loc.Get("Music.Empty"), false, 100, false);
            Playlist.Update += (sender, args) => UpdateQueue();
            CurrentTrackIndexChange += (sender, args) => UpdateQueue();
            UpdateTrackInfo();
            UpdateProgress();
            UpdateQueue();
            UpdateParameters();
            Playlist.Update += (sender, args) => UpdateQueueMessageContent();
            _queueHistory.HistoryChanged += (sender, args) => {
                EmbedBuilder.Fields["RequestHistory"].Value = _queueHistory.GetLastHistory(Loc, out var isChanged).IsEmpty(Loc.Get("Music.Empty"));
                if (isChanged) UpdateControlMessage();
            };
        }

        public IUserMessage? ControlMessage { get; private set; }

        public override async Task SetVolumeAsync(int volume = 100) {
            await base.SetVolumeAsync(volume);
            UpdateParameters();
        }

        public override void SetBassBoostMode(BassBoostMode mode) {
            base.SetBassBoostMode(mode);
            UpdateParameters();
        }

        public override async Task OnTrackStartedAsync(TrackStartedEventArgs eventArgs) {
            UpdatePlayback = true;
            UpdateTrackInfo();
        }

        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            if (eventArgs.Reason == TrackEndReason.LoadFailed) {
                WriteToQueueHistory(Loc.Get(CurrentTrack!.Identifier == LoadFailedId ? "Music.DecodingErrorRemove" : "Music.DecodingError",
                    MusicUtils.EscapeTrack(CurrentTrack.Title).SafeSubstring(40, "...") ?? ""));
            }

            await base.OnTrackEndAsync(eventArgs);

            UpdateTrackInfo();
            await UpdateControlMessage();
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (State) {
                case PlayerState.NotPlaying:
                case PlayerState.Destroyed:
                case PlayerState.NotConnected:
                    UpdateProgress();
                    UpdatePlayback = false;
                    break;
            }
        }

        public override async Task PauseAsync() {
            await base.PauseAsync();
            UpdatePlayback = false;
            UpdateProgress();
        }

        public override async Task ResumeAsync() {
            await base.ResumeAsync();
            UpdatePlayback = true;
            UpdateProgress();
        }

        public override async Task ExecuteShutdown(IEntry reason, PlayerShutdownParameters parameters) {
            await base.ExecuteShutdown(reason, parameters);

            var oldControlMessage = ControlMessage;
            ControlMessage = null;
            if (oldControlMessage != null && !parameters.LeaveMessageUnchanged) {
                var embedBuilder = new EmbedBuilder().WithTitle(Loc.Get("Music.PlaybackStopped")).WithDescription(reason.Get(Loc));
                if (parameters.AddResumeToMessage) {
                    if (parameters.StoredPlaylist != null) {
                        embedBuilder.Description += Loc.Get("Music.ResumeViaPlaylists", GuildConfig.Prefix, parameters.StoredPlaylist.Id);
                    }
                    else {
                        oldControlMessage.DelayedDelete(Constants.LongTimeSpan);
                    }
                }

                if (_updateControlMessageTask.IsExecuting) await _updateControlMessageTask.Execute(false);
                oldControlMessage.ModifyAsync(properties => {
                    properties.Embed = embedBuilder.Build();
                    properties.Content = null;
                });
                oldControlMessage.RemoveAllReactionsAsync();
            }

            _collectorsGroup.DisposeAll();
            _queueMessage?.StopAndClear();

            UpdatePlayback = false;
        }

        public override void GetPlayerShutdownParameters(PlayerShutdownParameters parameters) {
            base.GetPlayerShutdownParameters(parameters);
            parameters.LastControlMessage = ControlMessage;
        }

        public override void WriteToQueueHistory(HistoryEntry entry) {
            _queueHistory.Add(entry);
        }

        public override void WriteToQueueHistory(string entry) {
            _queueHistory.Add(new HistoryEntry(new EntryString(entry)));
        }

        public Task SetControlMessage(IUserMessage message) {
            ControlMessage?.SafeDelete();
            ControlMessage = message;
            return Task.WhenAll(SetupControlReactions(), UpdateControlMessage(), SetupWarnings());
        }

        private async Task SetupWarnings() {
            if (ControlMessage != null) {
                var guildUser = (await Guild.GetUserAsync(Program.Client.CurrentUser.Id)).GetPermissions((IGuildChannel) ControlMessage.Channel);
                IsExternalEmojiAllowed = guildUser.UseExternalEmojis;
                var text = "";
                if (!guildUser.ManageMessages) text += Loc.Get("Music.WarningEmojiRemoval") + "\n";
                if (!guildUser.AddReactions) text += Loc.Get("Music.WarningEmojiAdding") + "\n";
                if (!guildUser.UseExternalEmojis) text += Loc.Get("Music.WarningCustomEmoji") + "\n";

                // ReSharper disable once AssignmentInConditionalExpression
                if (EmbedBuilder.Fields["Warnings"].IsEnabled = !string.IsNullOrWhiteSpace(text)) {
                    EmbedBuilder.Fields["Warnings"].Value = text;
                }
            }
        }

        public override async Task Enqueue(List<AuthoredTrack> tracks, int position = -1) {
            await base.Enqueue(tracks, position);
            if (tracks.Count == 1) {
                var track = tracks.First();
                WriteToQueueHistory(Loc.Get("MusicQueues.Enqueued").Format(track.GetRequester(), MusicUtils.EscapeTrack(track.Title)));
            }
            else if (tracks.Count > 1) {
                var author = tracks.First().GetRequester();
                WriteToQueueHistory(Loc.Get("MusicQueues.EnqueuedMany").Format(author, tracks.Count));
            }
        }

        public override Task OnTrackLimitExceed(string author, int count) {
            WriteToQueueHistory(Loc.Get("MusicQueues.LimitExceed", author, count, Constants.MaxTracksCount));
            return base.OnTrackLimitExceed(author, count);
        }

        public async Task PrintQueue(IMessageChannel targetChannel) {
            var paginatedAppearanceOptions = new PaginatedAppearanceOptions {Timeout = TimeSpan.FromMinutes(1)};
            _queueMessage ??= new PaginatedMessage(paginatedAppearanceOptions, targetChannel, Loc) {
                Title = Loc.Get("MusicQueues.QueueTitle"), Color = Color.Gold
            };

            UpdateQueueMessageContent();

            _queueMessage.Resend();
        }

        private void UpdateQueueMessageContent() {
            _queueMessage?.SetPages(
                string.Join("\n",
                    Playlist.Select((track, i) => (CurrentTrackIndex == i ? "@" : " ") + $"{i + 1}: {MusicUtils.EscapeTrack(Playlist[i].Title)}")),
                "```py\n{0}```", 50);
        }

        #region Playlists

        public override async Task ImportPlaylist(ExportPlaylist playlist, ImportPlaylistOptions options, string requester) {
            if (Playlist.Count + playlist.Tracks.Count > Constants.MaxTracksCount) {
                WriteToQueueHistory(Loc.Get("MusicQueues.PlaylistLoadingLimit", requester, playlist.Tracks.Count, Constants.MaxTracksCount));
                return;
            }

            var tracks = playlist.Tracks.Select(s => TrackDecoder.DecodeTrack(s))
                                 .Select(track => new AuthoredTrack(track, requester)).ToList();
            if (options == ImportPlaylistOptions.Replace) {
                try {
                    await StopAsync();
                    WriteToQueueHistory(Loc.Get("Music.ImportPlayerStop"));
                }
                catch (Exception) {
                    // ignored
                }

                if (!Playlist.IsEmpty) {
                    Playlist.Clear();
                    WriteToQueueHistory(Loc.Get("Music.ClearPlaylist").Format(requester));
                }
            }

            Playlist.AddRange(tracks);
            WriteToQueueHistory(Loc.Get("Music.AddTracks").Format(requester, tracks.Count));

            if (options != ImportPlaylistOptions.JustAdd) {
                var track = playlist.TrackIndex == -1 ? tracks.First() : tracks[playlist.TrackIndex.Normalize(0, playlist.Tracks.Count - 1)];
                var position = playlist.TrackPosition;
                if (position != null && position.Value > track.Duration) {
                    position = TimeSpan.Zero;
                }

                await PlayAsync(track, false, position);
                WriteToQueueHistory(Loc.Get("MusicQueues.Jumped")
                                       .Format(requester, CurrentTrackIndex + 1, MusicUtils.EscapeTrack(CurrentTrack!.Title).SafeSubstring(100, "...")));
            }
            else if (State == PlayerState.NotPlaying) {
                await PlayAsync(Playlist[0], false);
            }
        }

        #endregion

        #region Emoji

        private readonly CollectorsGroup _collectorsGroup = new CollectorsGroup();

        private Task SetupControlReactions() {
            _collectorsGroup.DisposeAll();
            if (ControlMessage == null) return Task.CompletedTask;
            _collectorsGroup.Add(
                CollectorsUtils.CollectReactions<string>(
                    reaction => reaction.MessageId == ControlMessage?.Id && reaction.UserId != Program.Client.CurrentUser.Id,
                    async (args, s) => {
                        args.RemoveReason();
                        await Program.Handler.ExecuteCommand(s, new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                    },
                    (CommonEmoji.LegacyTrackPrevious, () => TrackPosition.TotalSeconds > 15 ? "seek 0s" : "skip -1"),
                    (CommonEmoji.LegacyPlay, () => "resume"),
                    (CommonEmoji.LegacyPause, () => "pause"),
                    (CommonEmoji.LegacyTrackNext, () => "skip"),
                    (CommonEmoji.LegacyStop, () => "stop"),
                    (CommonEmoji.LegacyRepeat, () => "repeat"),
                    (CommonEmoji.LegacyShuffle, () => "shuffle")
                ));
            _addReactionsAsync = ControlMessage.AddReactionsAsync(new IEmote[] {
                CommonEmoji.LegacyTrackPrevious, CommonEmoji.LegacyPlay, CommonEmoji.LegacyPause, CommonEmoji.LegacyTrackNext,
                CommonEmoji.LegacyStop, CommonEmoji.LegacyRepeat, CommonEmoji.LegacyShuffle
            });

            _collectorsGroup.Controllers.Add(CollectorsUtils.CollectMessage(ControlMessage.Channel, message => true, async args => {
                args.StopCollect();
                try {
                    await _addReactionsAsync;
                }
                catch (Exception) {
                    // ignored
                }

                if (ControlMessage == null) return;
                // Theoretically (Control Message == null) - impossible
                ControlMessage.AddReactionAsync(CommonEmoji.LegacyArrowDown);
                _collectorsGroup.Controllers.Add(CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyArrowDown), async emoteCollectorEventArgs => {
                        emoteCollectorEventArgs.RemoveReason();
                        if ((await ControlMessage.Channel.GetMessagesAsync(1).FlattenAsync()).FirstOrDefault()?.Id == ControlMessage.Id) {
                            return;
                        }

                        await Program.Handler.ExecuteCommand($"play",
                            new ReactionCommandContext(Program.Client, emoteCollectorEventArgs.Reaction),
                            emoteCollectorEventArgs.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf));
            }));

            return _addReactionsAsync;
        }

        #endregion

        #region Embed updates

        private readonly SingleTask _controlMessageSendTask;
        private IMessageChannel _controlMessageChannel = null!;

        public async Task EnqueueControlMessageSend(IMessageChannel? channel = null) {
            _controlMessageChannel = channel ?? _controlMessageChannel;
            _controlMessageSendTask.Execute();
        }

        public override void UpdatePlayer() {
            base.UpdatePlayer();
            if (UpdatePlayback) {
                UpdateProgress();
            }
        }

        private readonly SingleTask _updateControlMessageTask;
        private Task? _addReactionsAsync;
        private PaginatedMessage? _queueMessage;

        private async Task UpdateControlMessage(bool background = false) {
            if (State != PlayerState.Destroyed) _updateControlMessageTask.Execute(!background);
        }

        public void UpdateProgress(bool background = false) {
            if (CurrentTrack != null) {
                EmbedBuilder.Fields["State"].Name = Loc.Get("Music.RequestedBy").Format(CurrentTrack.GetRequester());

                var progressPercentage = Convert.ToInt32(TrackPosition.TotalSeconds / CurrentTrack.Duration.TotalSeconds * 100);
                var progressBar = (IsExternalEmojiAllowed ? ProgressEmoji.CustomEmojiPack : ProgressEmoji.TextEmojiPack).GetProgress(progressPercentage);

                var stateString = State switch {
                    PlayerState.Playing => IsExternalEmojiAllowed ? CommonEmojiStrings.Instance.Play : "▶",
                    PlayerState.Paused  => IsExternalEmojiAllowed ? CommonEmojiStrings.Instance.Pause : "⏸",
                    _                   => IsExternalEmojiAllowed ? CommonEmojiStrings.Instance.Stop : "⏹"
                };
                var loopingStateString = LoopingState switch {
                    LoopingState.One => IsExternalEmojiAllowed ? CommonEmojiStrings.Instance.RepeatOnce : "🔂",
                    LoopingState.All => IsExternalEmojiAllowed ? CommonEmojiStrings.Instance.Repeat : "🔁",
                    LoopingState.Off => IsExternalEmojiAllowed ? CommonEmojiStrings.Instance.RepeatOff : "❌",
                    _                => throw new InvalidEnumArgumentException()
                };
                var spotifyId = (CurrentTrack is AuthoredTrack authoredTrack && authoredTrack.Track is SpotifyLavalinkTrack spotifyLavalinkTrack)
                    ? spotifyLavalinkTrack.RelatedSpotifyTrack.Id
                    : null;
                var spotifyEmojiExists = spotifyId != null && IsExternalEmojiAllowed;
                var sb = new StringBuilder(TrackPosition.FormattedToString());
                if (CurrentTrack.IsSeekable) {
                    sb.Append(" / ");
                    sb.Append(CurrentTrack.Duration.FormattedToString());
                }

                var space = new string(' ', Math.Max(0, ((spotifyEmojiExists ? 18 : 22) - sb.Length) / 2));
                var detailsBar = stateString + '`' + space + sb + space + '`' + loopingStateString;
                if (spotifyEmojiExists) detailsBar += $"[{CommonEmojiStrings.Instance.Spotify}](https://open.spotify.com/track/{spotifyId})";
                EmbedBuilder.Fields["State"].Value = progressBar + "\n" + detailsBar;
            }
            else {
                EmbedBuilder.Fields["State"].Name = Loc.Get("Music.Playback");
                EmbedBuilder.Fields["State"].Value = Loc.Get("Music.PlaybackNothingPlaying");
            }

            UpdateControlMessage(background);
        }

        public void UpdateTrackInfo() {
            if (CurrentTrackIndex >= Playlist.Count && Playlist.Count != 0) {
                EmbedBuilder.Author = new EmbedAuthorBuilder();
                EmbedBuilder.Title = Loc.Get("Music.QueueEnd");
                EmbedBuilder.Url = "";
            }
            else if ((State != PlayerState.NotPlaying || State != PlayerState.NotConnected || State != PlayerState.Destroyed) && CurrentTrack != null) {
                var iconUrl = CurrentTrack.Provider == StreamProvider.YouTube ? $"https://img.youtube.com/vi/{CurrentTrack?.TrackIdentifier}/0.jpg" : null;
                EmbedBuilder
                   .WithAuthor(CurrentTrack!.Author.SafeSubstring(Constants.MaxEmbedAuthorLength, "...").IsEmpty("Unknown"), iconUrl)
                   .WithTitle(MusicUtils.EscapeTrack(CurrentTrack!.Title).SafeSubstring(Discord.EmbedBuilder.MaxTitleLength, "...")!)
                   .WithUrl(CurrentTrack.Source!);
            }
            else {
                EmbedBuilder.Author = new EmbedAuthorBuilder();
                EmbedBuilder.Title = Loc.Get("Music.Waiting");
                EmbedBuilder.Url = "";
            }
        }

        public void UpdateParameters() {
            var volumeText = GuildConfig.Volume < 50 || GuildConfig.Volume > 120 ? $"🔉 ***{GuildConfig.Volume}%***\n" : $"🔉 {GuildConfig.Volume}%\n";
            EmbedBuilder.Fields["Parameters"].Value = volumeText + $"🅱️ {BassBoostMode}";
            UpdateControlMessage();
        }

        private void UpdateQueue() {
            if (Playlist.Count == 0) {
                EmbedBuilder.Fields["Queue"].Name = Loc.Get("Music.QueueEmptyTitle");
                EmbedBuilder.Fields["Queue"].Value = Loc.Get("Music.QueueEmpty").Format(GuildConfig.Prefix);
            }
            else {
                EmbedBuilder.Fields["Queue"].Name =
                    Loc.Get("Music.Queue").Format(CurrentTrackIndex + 1, Playlist.Count, Playlist.TotalPlaylistLength.FormattedToString());
                EmbedBuilder.Fields["Queue"].Value = $"```py\n{GetPlaylistString()}```";
            }

            UpdateControlMessage();

            StringBuilder GetPlaylistString() {
                var globalStringBuilder = new StringBuilder();
                string? lastAuthor = null;
                var authorStringBuilder = new StringBuilder();
                for (var i = Math.Max(CurrentTrackIndex - 1, 0); i < CurrentTrackIndex + 5; i++) {
                    if (!Playlist.TryGetValue(i, out var track)) continue;
                    var author = track is AuthoredTrack authoredLavalinkTrack ? authoredLavalinkTrack.GetRequester() : "Unknown";
                    if (author != lastAuthor && lastAuthor != null) FinalizeBlock();
                    authorStringBuilder.Replace("└", "├").Replace("▬", "│");
                    authorStringBuilder.Append(GetTrackString(MusicUtils.EscapeTrack(track!.Title),
                        i + 1, CurrentTrackIndex == i));
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

        public override async Task NodeChanged(LavalinkNode? node = null) {
            node ??= MusicUtils.Cluster!.GetServingNode(GuildId);
            EmbedBuilder.WithFooter($"Powered by {Program.Client.CurrentUser.Username} | {node.Label}");
            base.NodeChanged(node);
        }

        #endregion
    }
}