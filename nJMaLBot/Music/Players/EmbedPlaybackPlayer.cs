using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Config.Localization.Providers;
using Bot.Music.Players;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.Emoji;
using Bot.Utilities.Modules;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Decoding;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using LiteDB;
using Timer = System.Threading.Timer;

#pragma warning disable 4014

namespace Bot.Music {
    public sealed class EmbedPlaybackPlayer : PlaylistLavalinkPlayer {
        private string _playlistString;
        public bool UpdatePlayback = false;
        private EmbedBuilder EmbedBuilder = new EmbedBuilder();
        public IUserMessage ControlMessage { get; private set; }
        private bool IsConstructing { get; set; } = true;
        public readonly ILocalizationProvider Loc;
        private StringBuilder _queueHistory = new StringBuilder();

        // ReSharper disable once UnusedParameter.Local
        public EmbedPlaybackPlayer(ulong guildId) : base(guildId) {
            Loc = new GuildLocalizationProvider(guildId);
            EmbedBuilder.AddField("Placeholder", "Placeholder", true);
            EmbedBuilder.AddField(Loc.Get("Music.Parameters"), "Placeholder", true);
            EmbedBuilder.AddField(Loc.Get("Music.Queue").Format(0, 0), "Placeholder");
            EmbedBuilder.AddField(Loc.Get("Music.RequestHistory"), "Placeholder");
            Playlist.Update += (sender, args) => UpdatePlaylist();
            CurrentTrackIndexChange += (sender, args) => UpdatePlaylist();
        }

        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false) {
            await base.SetVolumeAsync(volume, normalize);
            UpdateParameters();
        }

        public override async Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await base.OnConnectedAsync(voiceServer, voiceState);
            UpdateParameters();
        }

        public override Task OnTrackStartedAsync(TrackStartedEventArgs eventArgs) {
            UpdatePlayback = true;
            var toReturn = base.OnTrackStartedAsync(eventArgs);
            UpdateTrackInfo();
            return toReturn;
        }

        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            if (eventArgs.Reason == TrackEndReason.LoadFailed) {
                WriteToQueueHistory(Loc.Get("Music.DecodingError").Format(CurrentTrack.Title));
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

        public async Task Dispose(LocalizedEntry reason, bool needSave = true) {
            await Dispose(reason.Get(Loc), needSave);
        }

        public async Task Dispose(string reason, bool needSave = true) {
            if (ControlMessage != null) {
                var oldControlMessage = ControlMessage;
                ControlMessage = null;
                var embedBuilder = new EmbedBuilder()
                                  .WithTitle(Loc.Get("Music.PlaybackStopped"))
                                  .WithDescription(reason);
                if (needSave) {
                    var exportPlaylist = ExportPlaylist(ExportPlaylistOptions.AllData);
                    var storedPlaylist = exportPlaylist.StorePlaylist("a" + ObjectId.NewObjectId(), 0);
                    embedBuilder.Description += Loc.Get("Music.ResumeViaPlaylists").Format(GuildConfig.Get(GuildId).Prefix, storedPlaylist.Id);
                }
                else {
                    oldControlMessage.DelayedDelete(TimeSpan.FromMinutes(10));
                }

                await _modifyAsync;
                oldControlMessage?.ModifyAsync(properties => {
                    properties.Embed = embedBuilder.Build();
                    properties.Content = null;
                });
                _collectorsGroup?.DisposeAll();
                oldControlMessage?.RemoveAllReactionsAsync();
            }
            
            _queueCollectorsGroup?.DisposeAll();
            _queueMessage?.SafeDelete();

            UpdatePlayback = false;

            try {
                EmbedPlaybackControl.PlaybackPlayers.Remove(this);
                base.Dispose();
            }
            catch (Exception) {
                // ignored
            }
        }

        public override void Dispose() {
            Dispose(Loc.Get("Music.PlaybackStopped"));
            base.Dispose();
        }

        public override async Task<int> PlayAsync(LavalinkTrack track, bool enqueue, TimeSpan? startTime = null, TimeSpan? endTime = null,
                                                  bool noReplace = false) {
            var toReturn = await base.PlayAsync(track, enqueue, startTime, endTime, noReplace);

            if (IsConstructing) {
                IsConstructing = false;
                SetupControlReactions();
            }

            UpdateProgress();

            UpdatePlayback = true;
            return toReturn;
        }

        public void WriteToQueueHistory(string entry, bool background = false) {
            _queueHistory.AppendLine("- " + entry);
            while (_queueHistory.Length > 512) {
                var indexOf = _queueHistory.ToString().IndexOf(Environment.NewLine, StringComparison.Ordinal);
                if (indexOf >= 0) _queueHistory.Remove(0, indexOf + Environment.NewLine.Length);
            }

            EmbedBuilder.Fields[3].Value = _queueHistory.ToString().Replace("\n\n", "\n");
            UpdateControlMessage(background);
        }

        public Task SetControlMessage(IUserMessage message) {
            ControlMessage?.SafeDelete();
            ControlMessage = message;
            SetupControlReactions();
            SetupControlReactions();
            UpdateControlMessage();
            return Task.CompletedTask;
        }

        private string GetPlaylistString(LavalinkPlaylist playlist, int index) {
            try {
                var globalStringBuilder = new StringBuilder();
                string lastAuthor = null;
                var authorStringBuilder = new StringBuilder();
                for (var i = Math.Max(index - 1, 0); i < index + 5; i++) {
                    if (!playlist.TryGetValue(i, out var track)) continue;
                    var author = (track is AuthoredLavalinkTrack authoredLavalinkTrack) ? authoredLavalinkTrack.GetRequester() : "Unknown";
                    if (author != lastAuthor && lastAuthor != null) FinalizeBlock();
                    authorStringBuilder.Replace("└", "├").Replace("▬", "│");
                    authorStringBuilder.Append(GetTrackString(track.Title.Replace("'", "").Replace("#", ""),
                        i + 1, 40, CurrentTrackIndex == i));
                    lastAuthor = author;
                }

                FinalizeBlock();

                void FinalizeBlock() {
                    globalStringBuilder.AppendLine($"────┬────{lastAuthor}");
                    globalStringBuilder.Append(authorStringBuilder.Replace("▬", " "));

                    authorStringBuilder.Clear();
                }

                StringBuilder GetTrackString(string title, int trackNumber, int maxLength, bool isCurrent) {
                    var lines = Utilities.Utilities.SplitToLines(title, maxLength).ToList();
                    var sb = new StringBuilder();
                    sb.AppendLine($"{(isCurrent ? "@" : " ")}{trackNumber}   ".SafeSubstring(0, 4) + "└" + lines.First());
                    foreach (var line in lines.Skip(1)) {
                        sb.AppendLine((isCurrent ? "@" : " ").PadRight(4) + "▬" + line.SafeSubstring(0, 40));
                    }

                    return sb;
                }

                return $"```py\n{globalStringBuilder}```";
            }
            catch (Exception) {
                return "Failed";
            }
        }

        public override async Task Enqueue(List<AuthoredLavalinkTrack> tracks, bool enqueue) {
            await base.Enqueue(tracks, enqueue);
            if (tracks.Count == 1) {
                var track = tracks.First();
                WriteToQueueHistory(Loc.Get("MusicQueues.Enqueued").Format(track.GetRequester(), MusicUtils.EscapeTrack(track.Title)), true);
            }
            else if (tracks.Count > 1) {
                var author = tracks.First().GetRequester();
                WriteToQueueHistory(Loc.Get("MusicQueues.EnqueuedMany").Format(author, tracks.Count), true);
            }
        }

        public override Task OnTrackLimitExceed(string author, int count) {
            WriteToQueueHistory(Loc.Get("MusicQueues.LimitExceed").Format(author, count));
            return base.OnTrackLimitExceed(author, count);
        }

        public async Task PrintQueue(IUserMessage loadingMessage) {
            _queueMessage?.SafeDelete();
            _queueMessage = loadingMessage;
            var pageNumber = 0;
            var queuePages = GetQueuePages();
            var embedBuilder = new EmbedBuilder().WithColor(Color.Gold).WithTitle(Loc.Get("MusicQueues.QueueTitle"));
            UpdateEmbed();
            await ModifyMessage();

            _queueCollectorsGroup?.DisposeAll();
            _queueCollectorsGroup = new CollectorsGroup(
                CollectorsUtils.CollectReaction(loadingMessage, reaction => reaction.Emote.Equals(CommonEmoji.LegacyTrackPrevious), async args => {
                    args.RemoveReason();
                    pageNumber = 0;
                    UpdateEmbed();
                    await ModifyMessage();
                }, CollectorFilter.IgnoreBots),
                CollectorsUtils.CollectReaction(loadingMessage, reaction => reaction.Emote.Equals(CommonEmoji.LegacyTrackNext), async args => {
                    args.RemoveReason();
                    pageNumber = queuePages.Count - 1;
                    UpdateEmbed();
                    await ModifyMessage();
                }, CollectorFilter.IgnoreBots),
                CollectorsUtils.CollectReaction(loadingMessage, reaction => reaction.Emote.Equals(CommonEmoji.LegacyPlay), async args => {
                    args.RemoveReason();
                    pageNumber = (pageNumber + 1).Normalize(0, queuePages.Count - 1);
                    UpdateEmbed();
                    await ModifyMessage();
                }, CollectorFilter.IgnoreBots),
                CollectorsUtils.CollectReaction(loadingMessage, reaction => reaction.Emote.Equals(CommonEmoji.LegacyReverse), async args => {
                    args.RemoveReason();
                    pageNumber = (pageNumber - 1).Normalize(0, queuePages.Count - 1);
                    UpdateEmbed();
                    await ModifyMessage();
                }, CollectorFilter.IgnoreBots),
                CollectorsUtils.CollectReaction(loadingMessage, reaction => reaction.Emote.Equals(CommonEmoji.LegacyFileBox), async args => {
                    args.RemoveReason();
                    await args.Reaction.Channel.SendTextAsFile(string.Join("", queuePages),
                                   $"Playlist {DateTime.Now}, {(loadingMessage.Channel as IGuildChannel)?.Guild?.Name}.txt");
                    _queueCollectorsGroup?.DisposeAll();
                    _queueMessage.SafeDelete();
                }, CollectorFilter.IgnoreBots)
            );

            loadingMessage.AddReactionsAsync(new IEmote[]
                {CommonEmoji.LegacyTrackPrevious, CommonEmoji.LegacyReverse, CommonEmoji.LegacyFileBox, CommonEmoji.LegacyPlay, CommonEmoji.LegacyTrackNext});

            this.QueueDeprecated += QueueDeprecated;

            async void QueueDeprecated(object? sender, EventArgs args) {
                try {
                    this.QueueDeprecated -= QueueDeprecated;
                    _queueCollectorsGroup.DisposeAll();
                    embedBuilder.WithDescription(Loc.Get("MusicQueues.QueueDeprecated").Format(GuildConfig.Prefix));
                    embedBuilder.Fields.Clear();
                    await ModifyMessage();
                }
                finally {
                    loadingMessage.DelayedDelete(TimeSpan.FromMinutes(2));
                }
            }

            void UpdateEmbed() {
                embedBuilder.WithDescription($"```py\n{queuePages[pageNumber]}```");
                if (embedBuilder.Fields.Count == 0) {
                    embedBuilder.AddField("Placeholder", "Placeholder");
                }
                embedBuilder.Fields[0] = new EmbedFieldBuilder {
                    Name = Loc.Get("MusicQueues.QueuePage").Format(pageNumber + 1, queuePages.Count),
                    Value = Loc.Get("MusicQueues.QueuePageDescription")
                };
            }

            async Task ModifyMessage() {
                await loadingMessage.ModifyAsync(properties => {
                    properties.Content = null;
                    properties.Embed = embedBuilder.Build();
                });
            }
        }

        #region Playlists

        public override async Task ImportPlaylist(ExportPlaylist playlist, ImportPlaylistOptions options, string requester) {
            if (Playlist.Count + playlist.Tracks.Count > 10000) {
                WriteToQueueHistory(Loc.Get("MusicQueues.PlaylistLoadingLimit").Format(requester, playlist.Tracks.Count));
                return;
            }

            var tracks = playlist.Tracks.Select(s => TrackDecoder.DecodeTrack(s))
                                 .Select(track => AuthoredLavalinkTrack.FromLavalinkTrack(track, requester)).ToList();
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
                                       .Format(requester, CurrentTrackIndex + 1, CurrentTrack.Title.SafeSubstring(0, 40) + "..."));
            }
            else if (State == PlayerState.NotPlaying) {
                await PlayAsync(Playlist[0], false);
            }
        }

        #endregion

        #region Emoji

        private CollectorsGroup _collectorsGroup;

        private void SetupControlReactions() {
            if (IsConstructing)
                return;

            _collectorsGroup?.DisposeAll();
            _collectorsGroup = new CollectorsGroup(
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyTrackPrevious), async args => {
                        args.RemoveReason();
                        await Program.Handler.ExecuteCommand("skip -1", new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyPlay), async args => {
                        args.RemoveReason();
                        await Program.Handler.ExecuteCommand("resume", new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyPause), async args => {
                        args.RemoveReason();
                        await Program.Handler.ExecuteCommand("pause", new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyTrackNext), async args => {
                        args.RemoveReason();
                        await Program.Handler.ExecuteCommand("skip", new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyStop), async args => {
                        args.RemoveReason();
                        await Program.Handler.ExecuteCommand("stop", new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyRepeat), async args => {
                        args.RemoveReason();
                        await Program.Handler.ExecuteCommand("repeat", new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyShuffle), async args => {
                        args.RemoveReason();
                        await Program.Handler.ExecuteCommand("shuffle", new ReactionCommandContext(Program.Client, args.Reaction),
                            args.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacySound), async args => {
                        args.RemoveReason();
                        await Program.Handler.ExecuteCommand($"volume {(int) ((Volume - 0.1f) * 100)}",
                            new ReactionCommandContext(Program.Client, args.Reaction), args.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyLoudSound), async args => {
                        args.RemoveReason();
                        await Program.Handler.ExecuteCommand($"volume {(int) ((Volume + 0.1f) * 100)}",
                            new ReactionCommandContext(Program.Client, args.Reaction), args.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf)
            );
            _addReactionsAsync = ControlMessage.AddReactionsAsync(new IEmote[] {
                CommonEmoji.LegacyTrackPrevious, CommonEmoji.LegacyPlay, CommonEmoji.LegacyPause, CommonEmoji.LegacyTrackNext,
                CommonEmoji.LegacyStop, CommonEmoji.LegacyRepeat, CommonEmoji.LegacyShuffle, CommonEmoji.LegacySound, CommonEmoji.LegacyLoudSound
            });

            CollectorsUtils.CollectMessage(ControlMessage.Channel, message => true, async args => {
                args.StopCollect();
                try {
                    await _addReactionsAsync;
                }
                catch (Exception) {
                    // ignored
                }

                ControlMessage.AddReactionAsync(CommonEmoji.LegacyArrowDown);
                _collectorsGroup.Controllers.Add(CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyArrowDown), async args => {
                        args.RemoveReason();
                        if ((await ControlMessage.Channel.GetMessagesAsync(1).FlattenAsync()).FirstOrDefault()?.Id == ControlMessage.Id) {
                            return;
                        }

                        await Program.Handler.ExecuteCommand($"play",
                            new ReactionCommandContext(Program.Client, args.Reaction), args.Reaction.UserId.ToString());
                    }, CollectorFilter.IgnoreSelf));
            });
        }

        #endregion

        #region Embed updates

        private Timer _controlMessageSendTimer;

        public async Task EnqueueControlMessageSend(IMessageChannel channel) {
            if (_controlMessageSendTimer == null) {
                await SetControlMessage(await channel.SendMessageAsync(Loc.Get("Music.Loading")));

                _controlMessageSendTimer = new Timer(state => { _controlMessageSendTimer = null; }, null, 5000, -1);
            }
            else {
                _controlMessageSendTimer = new Timer(async state => {
                    _controlMessageSendTimer = null;
                    await SetControlMessage(await channel.SendMessageAsync(Loc.Get("Music.Loading")));
                }, null, 5000, -1);
            }
        }

        public override void UpdatePlayer() {
            base.UpdatePlayer();
            if (UpdatePlayback) {
                UpdateProgress();
            }
        }

        private Task _modifyAsync;
        private bool _modifyQueued;
        private Task _addReactionsAsync;
        private CollectorsGroup _queueCollectorsGroup;
        private IUserMessage _queueMessage;

        private async Task UpdateControlMessage(bool background = false) {
            if (IsConstructing || ControlMessage == null)
                return;

            //Not thread safe method cuz in this case, thread safety is a waste of time
            if (this._modifyAsync?.IsCompleted ?? true) {
                UpdateInternal();
            }
            else if (!background) {
                if (_modifyQueued)
                    return;
                try {
                    _modifyQueued = true;
                    await this._modifyAsync;
                    UpdateInternal();
                }
                finally {
                    _modifyQueued = false;
                }
            }

            void UpdateInternal() {
                this._modifyAsync = ControlMessage?.ModifyAsync(properties => {
                    properties.Embed = EmbedBuilder.Build();
                    properties.Content = "";
                });
            }
        }

        public void UpdateProgress(bool background = false) {
            if (CurrentTrack != null) {
                var playingState = State switch {
                    PlayerState.Playing => CommonEmojiStrings.Instance.Play,
                    PlayerState.Paused  => CommonEmojiStrings.Instance.Pause,
                    _                   => CommonEmojiStrings.Instance.Stop
                };
                var repeatState = LoopingState switch {
                    LoopingState.One => CommonEmojiStrings.Instance.RepeatOnce,
                    LoopingState.All => CommonEmojiStrings.Instance.Repeat,
                    LoopingState.Off => CommonEmojiStrings.Instance.RepeatOff,
                    _                => ""
                };
                var progress = Convert.ToInt32(TrackPosition.TotalSeconds / CurrentTrack.Duration.TotalSeconds * 100);
                var requester = CurrentTrack is AuthoredLavalinkTrack authoredLavalinkTrack ? authoredLavalinkTrack.GetRequester() : "Unknown";
                EmbedBuilder.Fields[0].Name = Loc.Get("Music.RequestedBy").Format(requester);
                EmbedBuilder.Fields[0].Value = GetProgressString(progress) + "\n" + GetProgressInfo(playingState, repeatState);
            }
            else {
                EmbedBuilder.Fields[0].Name = Loc.Get("Music.Playback");
                EmbedBuilder.Fields[0].Value = Loc.Get("Music.PlaybackNothingPlaying");
            }

            UpdateControlMessage(background);

            string GetProgressInfo(string playingState, string repeatState) {
                var sb = new StringBuilder("");
                if ((int) TrackPosition.TotalHours != 0)
                    sb.Append((int) TrackPosition.TotalHours + ":");
                sb.Append($"{TrackPosition:mm':'ss} / ");
                if ((int) CurrentTrack.Duration.TotalHours != 0)
                    sb.Append((int) CurrentTrack.Duration.TotalHours + ":");
                sb.Append($"{CurrentTrack.Duration:mm':'ss}");
                var space = new string(' ', Math.Max(0, (22 - sb.Length) / 2));
                return playingState + '`' + space + sb + space + '`' + repeatState;
            }

            static string GetProgressString(int progress) {
                var builder = new StringBuilder();
                builder.Append(ProgressEmoji.Start.GetEmoji(progress));
                progress -= 10;
                for (var i = 0; i < 8; i++) {
                    builder.Append(ProgressEmoji.Intermediate.GetEmoji(progress));
                    progress -= 10;
                }

                builder.Append(ProgressEmoji.End.GetEmoji(progress));
                return builder.ToString();
            }
        }

        public void UpdateTrackInfo() {
            if (State != PlayerState.NotPlaying) {
                var iconUrl = CurrentTrack.Provider == StreamProvider.YouTube ? $"https://img.youtube.com/vi/{CurrentTrack?.TrackIdentifier}/0.jpg" : null;
                EmbedBuilder?.WithAuthor(string.IsNullOrWhiteSpace(CurrentTrack.Author) ? "Unknown" : CurrentTrack.Author.SafeSubstring(0, 250), iconUrl)
                            ?.WithTitle(CurrentTrack.Title.SafeSubstring(0, 250))?.WithUrl(CurrentTrack.Source);
            }
            else {
                EmbedBuilder.Author = null;
                EmbedBuilder.Title = Loc.Get("Music.Waiting");
                EmbedBuilder.Url = "";
            }
        }

        public void UpdateParameters() {
            EmbedBuilder.Fields[1].Value = $"🔉 {Convert.ToInt32(Volume * 100f)}%\n" +
                                           $"🅱️ {BassBoostMode}";
            UpdateControlMessage();
        }

        private void UpdatePlaylist() {
            _playlistString = GetPlaylistString(Playlist, CurrentTrackIndex);
            UpdateQueue();
        }

        private void UpdateQueue() {
            if (Playlist.Count == 0) {
                EmbedBuilder.Fields[2].Name = Loc.Get("Music.QueueEmptyTitle");
                EmbedBuilder.Fields[2].Value = Loc.Get("Music.QueueEmpty").Format(GuildConfig.Prefix);
            }
            else {
                EmbedBuilder.Fields[2].Name = Loc.Get("Music.Queue").Format(CurrentTrackIndex + 1, Playlist.Count);
                EmbedBuilder.Fields[2].Value = _playlistString;
            }

            UpdateControlMessage();
        }

        public void UpdateNodeName() {
            var currentNode = MusicUtils.Cluster.GetServingNode(GuildId);
            EmbedBuilder.WithFooter($"Powered by {Program.Client.CurrentUser.Username} | {currentNode.Label}");
        }

        #endregion
    }
}