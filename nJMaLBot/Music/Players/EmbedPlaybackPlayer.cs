using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Bot.Config;
using Bot.Music.Players;
using Bot.Utilities;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;

namespace Bot.Music {
    public sealed class EmbedPlaybackPlayer : PlaylistLavalinkPlayer {
        public EmbedPlaybackPlayer(LavalinkSocket lavalinkSocket, IDiscordClientWrapper client, ulong guildId, bool disconnectOnStop)
            : base(lavalinkSocket, client, guildId, disconnectOnStop) {
            Playlist.Update += (sender, args) => PlaylistString = GetPlaylistString(Playlist, CurrentTrackIndex);
            CurrentTrackIndexChange += (sender, args) => PlaylistString = GetPlaylistString(Playlist, args);
            UpdateTimer.Elapsed += (sender, args) => {
                BuildEmbedFields();
                ControlMessage?.ModifyAsync(properties => {
                    properties.Embed = EmbedBuilder.Build();
                    properties.Content = "";
                });
            };
        }

        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false) {
            EnsureNotDestroyed();
            EnsureConnected();

            await base.SetVolumeAsync(volume, normalize);
            GuildConfig.Get(GuildId).SetVolume(volume).Save();
        }

        public override async Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await base.SetVolumeAsync(GuildConfig.Get(GuildId).Volume);
            await base.OnConnectedAsync(voiceServer, voiceState);
        }

        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            UpdateTimer.Stop();
            await base.OnTrackEndAsync(eventArgs);
            if (State == PlayerState.Playing) {
                EmbedBuilder?.WithAuthor(string.IsNullOrWhiteSpace(CurrentTrack.Author) ? "Unknown" : CurrentTrack.Author,
                                  $"https://img.youtube.com/vi/{(string.IsNullOrWhiteSpace(CurrentTrack.TrackIdentifier) ? "" : CurrentTrack.TrackIdentifier)}/0.jpg")
                            ?.WithThumbnailUrl(
                                  $"https://img.youtube.com/vi/{(string.IsNullOrWhiteSpace(CurrentTrack.TrackIdentifier) ? "" : CurrentTrack.TrackIdentifier)}/0.jpg")
                            ?.WithTitle(CurrentTrack.Title);
                ControlMessage?.ModifyAsync(properties => properties.Embed = EmbedBuilder.Build());
            }
            else if (State != PlayerState.Paused) {
                if (eventArgs.Reason == TrackEndReason.Finished) { }
            }
        }

        public override void Cleanup() {
            UpdateTimer.Stop();
            base.Cleanup();
        }

        public override async Task<int> PlayAsync(LavalinkTrack track, bool enqueue, TimeSpan? startTime = null, TimeSpan? endTime = null,
                                                  bool noReplace = false) {
            var initialState = State;
            var toReturn = base.PlayAsync(track, enqueue, startTime, endTime, noReplace);
            var guildConfig = GuildConfig.Get(GuildId);
            if (initialState != PlayerState.Playing && guildConfig.GetChannel(ChannelFunction.Music, out var channel)) {
                var iconUrl = $"https://img.youtube.com/vi/{(string.IsNullOrWhiteSpace(track.TrackIdentifier) ? "" : track.TrackIdentifier)}/0.jpg";
                EmbedBuilder?.WithAuthor(string.IsNullOrWhiteSpace(track.Author) ? "Unknown" : track.Author, iconUrl)
                            ?.WithThumbnailUrl(iconUrl)?.WithTitle(track.Title)?.WithUrl(track.Source);
                BuildEmbedFields();
                ControlMessage ??= await ((ITextChannel) channel).SendMessageAsync("", false, EmbedBuilder.Build());
            }

            UpdateTimer.Start();
            return await toReturn;
        }

        private void BuildEmbedFields() {
            EmbedBuilder.Fields.Clear();
            var progress = Convert.ToInt32(TrackPosition.TotalSeconds / CurrentTrack.Duration.TotalSeconds * 100);
            var requester = CurrentTrack is AuthoredLavalinkTrack authoredLavalinkTrack ? authoredLavalinkTrack.GetRequester() : "Unknown";
            EmbedBuilder.AddField(
                $"Requested by: {requester}",
                GetProgressString(progress) + $"  `{TrackPosition:mm':'ss} / {CurrentTrack.Duration:mm':'ss}`", true);
            EmbedBuilder.AddField(Localization.Get(GuildId, "Music.Volume"), $"{Convert.ToInt32(Volume * 100f)}% 🔉", true);
            EmbedBuilder.AddField("Queue", PlaylistString);
        }

        private string GetProgressString(int progress) {
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
                    authorStringBuilder.Replace("└", "├");
                    authorStringBuilder.AppendLine(index == i
                        ?$"@{i + 1}   ".SafeSubstring(0, 4) + $"└{authoredLavalinkTrack.Title.SafeSubstring(0, 40).Trim()}"
                        :$" {i + 1}   ".SafeSubstring(0, 4) + $"└{authoredLavalinkTrack.Title.SafeSubstring(0, 40).Trim()}");
                    lastAuthor = author;
                }

                FinalizeBlock();

                void FinalizeBlock() {
                    globalStringBuilder.AppendLine($"────┬────{lastAuthor}");
                    globalStringBuilder.Append(authorStringBuilder);

                    authorStringBuilder = new StringBuilder();
                }


                return $"```py\n{globalStringBuilder}```";
            }
            catch (Exception) {
                return "Failed";
            }
        }

        private string PlaylistString;
        private Timer UpdateTimer = new Timer(TimeSpan.FromSeconds(4).TotalMilliseconds);
        private EmbedBuilder EmbedBuilder = new EmbedBuilder();
        private IUserMessage ControlMessage;

        public void SetControlMessage(IUserMessage message) {
            ControlMessage = (IUserMessage) message;
        }
    }
}