using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Config.Localization.Providers;
using Bot.Music.Players;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.Emoji;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using Tyrrrz.Extensions;

namespace Bot.Music {
    public sealed class EmbedPlaybackPlayer : PlaylistLavalinkPlayer {
        private string _playlistString;
        private Timer UpdateTimer = new Timer(TimeSpan.FromSeconds(4).TotalMilliseconds);
        private EmbedBuilder EmbedBuilder = new EmbedBuilder();
        public IUserMessage ControlMessage { get; private set; }
        private bool IsConstructing { get; set; } = true;
        public readonly ILocalizationProvider Loc;
        private StringBuilder _queueHistory = new StringBuilder();

        // ReSharper disable once UnusedParameter.Local
        public EmbedPlaybackPlayer(LavalinkSocket lavalinkSocket, IDiscordClientWrapper client, ulong guildId, bool disconnectOnStop)
            : base(lavalinkSocket, client, guildId, disconnectOnStop) {
            Loc = new GuildLocalizationProvider(GuildId);
            EmbedBuilder.AddField("Placeholder", "Placeholder", true);
            EmbedBuilder.AddField(Loc.Get("Music.Volume"), "Placeholder", true);
            EmbedBuilder.AddField(Loc.Get("Music.Queue").Format(0, 0), "Placeholder");
            EmbedBuilder.AddField(Loc.Get("Music.RequestHistory"), "Placeholder");
            Playlist.Update += (sender, args) => UpdatePlaylist();
            CurrentTrackIndexChange += (sender, args) => UpdatePlaylist();
            UpdateTimer.Elapsed += (sender, args) => UpdateProgress();
        }

        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false) {
            EnsureNotDestroyed();
            EnsureConnected();

            if (normalize)
                volume = Math.Min(Math.Max(volume, 0), 1.5f);
            await base.SetVolumeAsync(volume, normalize);
            GuildConfig.Get(GuildId).SetVolume(volume).Save();
            UpdateVolume();
        }

        public override async Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await base.SetVolumeAsync(GuildConfig.Get(GuildId).Volume);
            UpdateVolume();
            await base.OnConnectedAsync(voiceServer, voiceState);
        }

        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            if (eventArgs.Reason == TrackEndReason.LoadFailed) {
                var embedBuilder = new EmbedBuilder();
                embedBuilder.WithColor(Color.Red).WithTitle(Loc.Get("Music.TrackError"))
                            .WithDescription(Loc.Get("Music.DecodingError").Format(CurrentTrack.Title, CurrentTrack.Source));
                await ControlMessage.Channel.SendMessageAsync(null, false, embedBuilder.Build());
            }

            await base.OnTrackEndAsync(eventArgs);
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (State) {
                case PlayerState.NotPlaying:
                    UpdateProgress();
                    UpdateTimer.Stop();
                    break;
                case PlayerState.Destroyed:
                    UpdateProgress();
                    UpdateTimer.Stop();
                    break;
                case PlayerState.NotConnected:
                    UpdateProgress();
                    UpdateTimer.Stop();
                    break;
            }
        }

        public override async Task PauseAsync() {
            await base.PauseAsync();
            UpdateTimer.Stop();
            UpdateProgress();
        }

        public override async Task ResumeAsync() {
            await base.ResumeAsync();
            UpdateTimer.Start();
            UpdateProgress();
        }

        public override void Dispose() {
            Cleanup();
            try {
                base.Dispose();
            }
            catch (Exception) {
                // ignored
            }
        }

        public override void Cleanup() {
            UpdateTimer.Stop();
            if (ControlMessage != null) {
                var embedBuilder = new EmbedBuilder();
                embedBuilder.WithTitle(Loc.Get("Music.Playback"))
                            .WithDescription(Loc.Get("Music.PlaybackDisposed"))
                            .WithColor(Color.Gold);
                ControlMessage?.ModifyAsync(properties => {
                    properties.Embed = embedBuilder.Build();
                    properties.Content = null;
                });
                _collectorsGroup?.DisposeAll();
                ControlMessage?.RemoveAllReactionsAsync();
                ControlMessage?.DelayedDelete(TimeSpan.FromMinutes(10));
                ControlMessage = null;
            }

            base.Cleanup();
        }

        public void PrepareShutdown(string reason) {
            var embedBuilder = new EmbedBuilder();
            embedBuilder.WithTitle(Loc.Get("Music.PlaybackStopped"))
                        .WithDescription(reason);
            ControlMessage?.ModifyAsync(properties => {
                properties.Embed = embedBuilder.Build();
                properties.Content = null;
            });
            _collectorsGroup?.DisposeAll();
            ControlMessage?.RemoveAllReactionsAsync();
            ControlMessage.DelayedDelete(TimeSpan.FromMinutes(10));
            ControlMessage = null;
        }

        public override async Task<int> PlayAsync(LavalinkTrack track, bool enqueue, TimeSpan? startTime = null, TimeSpan? endTime = null,
                                                  bool noReplace = false) {
            var toReturn = await base.PlayAsync(track, enqueue, startTime, endTime, noReplace);

            var iconUrl = $"https://img.youtube.com/vi/{(string.IsNullOrWhiteSpace(CurrentTrack.TrackIdentifier) ? "" : CurrentTrack.TrackIdentifier)}/0.jpg";
            EmbedBuilder?.WithAuthor(string.IsNullOrWhiteSpace(CurrentTrack.Author) ? "Unknown" : CurrentTrack.Author.SafeSubstring(0, 250), iconUrl)
                        ?.WithThumbnailUrl(iconUrl)?.WithTitle(CurrentTrack.Title.SafeSubstring(0, 250))?.WithUrl(CurrentTrack.Source);
            if (IsConstructing) {
                IsConstructing = false;
                await SetupControlReactions();
            }

            UpdateProgress();

            UpdateTimer.Start();
            return toReturn;
        }

        private void UpdateControlMessage() {
            if (IsConstructing)
                return;
            ControlMessage?.ModifyAsync(properties => {
                properties.Embed = EmbedBuilder.Build();
                properties.Content = "";
            });
        }

        private void UpdateProgress() {
            var stateString = State switch {
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
            EmbedBuilder.Fields[0].Value =
                repeatState + stateString + GetProgressString(progress) + $"  `{TrackPosition:mm':'ss} / {CurrentTrack.Duration:mm':'ss}`";
            UpdateControlMessage();
        }

        private void UpdateVolume() {
            EmbedBuilder.Fields[1].Value = $"{Convert.ToInt32(Volume * 10f)}% 🔉";
            UpdateControlMessage();
        }

        private void UpdatePlaylist() {
            _playlistString = GetPlaylistString(Playlist, CurrentTrackIndex);
            UpdateQueue();
        }

        private void UpdateQueue() {
            EmbedBuilder.Fields[2].Name = Loc.Get("Music.Queue").Format(CurrentTrackIndex + 1, Playlist.Count);
            EmbedBuilder.Fields[2].Value = _playlistString;
            UpdateControlMessage();
        }

        public void WriteToQueueHistory(string entry) {
            _queueHistory.AppendLine("- " + entry);
            if (_queueHistory.Length > 512) {
                var indexOf = _queueHistory.ToString().IndexOf(Environment.NewLine, StringComparison.Ordinal);
                if (indexOf >= 0) _queueHistory.Remove(0, indexOf + Environment.NewLine.Length);
            }

            EmbedBuilder.Fields[3].Value = Utilities.Utilities.SplitToLines(_queueHistory.ToString(), 60)
                                                    .JoinToString("\n").Replace("\n\n", "\n");
            UpdateControlMessage();
        }

        public async Task SetControlMessage(IUserMessage message) {
            ControlMessage?.SafeDelete();
            ControlMessage = message;
            await SetupControlReactions();
            UpdateControlMessage();
        }

        private static string GetProgressString(int progress) {
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

        private string GetPlaylistString(LavalinkPlaylist playlist, int index) {
            try {
                var globalStringBuilder = new StringBuilder();
                string lastAuthor = null;
                var authorStringBuilder = new StringBuilder();
                for (var i = Math.Max(index - 1, 0); i < index + 5; i++) {
                    if (!playlist.TryGetValue(i, out var track) || !(track is AuthoredLavalinkTrack authoredLavalinkTrack)) continue;
                    var author = authoredLavalinkTrack.GetRequester();
                    if (author != lastAuthor && lastAuthor != null) FinalizeBlock();
                    authorStringBuilder.Replace("└", "├").Replace("▬", "│");
                    authorStringBuilder.Append(GetTrackString(authoredLavalinkTrack.Title.Replace("'", "").Replace("#", ""),
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

        private CollectorsGroup _collectorsGroup;

        private async Task SetupControlReactions() {
            if (IsConstructing)
                return;

            _collectorsGroup?.DisposeAll();
            await ControlMessage.AddReactionsAsync(new IEmote[] {
                CommonEmoji.LegacyTrackPrevious, CommonEmoji.LegacyPlay, CommonEmoji.LegacyPause, CommonEmoji.LegacyTrackNext,
                CommonEmoji.LegacyStop, CommonEmoji.LegacyRepeat, CommonEmoji.LegacySound, CommonEmoji.LegacyLoudSound
            });

            _collectorsGroup = new CollectorsGroup(
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyTrackPrevious), async args => {
                        args.RemoveReason();
                        await SkipAsync(-1, true);
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyPlay), async args => {
                        args.RemoveReason();
                        await TryResume();
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyPause), async args => {
                        args.RemoveReason();
                        await TryPause();
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyTrackNext), async args => {
                        args.RemoveReason();
                        await SkipAsync(1, true);
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyStop), args => {
                        args.RemoveReason();
                        StopAsync(true);
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyRepeat), args => {
                        args.RemoveReason();
                        LoopingState = LoopingState.Next();
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacySound), async args => {
                        args.RemoveReason();
                        await SetVolumeAsync(Volume - 0.1f, true);
                    }, CollectorFilter.IgnoreSelf),
                CollectorsUtils.CollectReaction(ControlMessage,
                    reaction => reaction.Emote.Equals(CommonEmoji.LegacyLoudSound), async args => {
                        args.RemoveReason(); 
                        await SetVolumeAsync(Volume + 0.1f, true);
                    }, CollectorFilter.IgnoreSelf)
            );
        }
    }
}