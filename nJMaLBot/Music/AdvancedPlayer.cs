using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Bot.Config;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Player;

namespace Bot.Music {
    public sealed class AdvancedPlayer : VoteLavalinkPlayer {
        public AdvancedPlayer(LavalinkSocket lavalinkSocket, IDiscordClientWrapper client, ulong guildId, bool disconnectOnStop)
            : base(lavalinkSocket, client, guildId, disconnectOnStop) { }

        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false) {
            EnsureNotDestroyed();
            EnsureConnected();

            await base.SetVolumeAsync(volume, normalize);
            GuildConfig.Get(GuildId).SetVolume(volume).Save();
        }

        public async override Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await base.SetVolumeAsync(GuildConfig.Get(GuildId).Volume);
            await base.OnConnectedAsync(voiceServer, voiceState);
        }

        public override Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            var toReturn = base.OnTrackEndAsync(eventArgs);
            if (State == PlayerState.Playing) {
                EmbedBuilder?.WithAuthor(string.IsNullOrWhiteSpace(CurrentTrack.Author) ? "Unknown" : CurrentTrack.Author,
                                  $"https://img.youtube.com/vi/{(string.IsNullOrWhiteSpace(CurrentTrack.TrackIdentifier) ? "" : CurrentTrack.TrackIdentifier)}/0.jpg")
                            ?.WithThumbnailUrl(
                                  $"https://img.youtube.com/vi/{(string.IsNullOrWhiteSpace(CurrentTrack.TrackIdentifier) ? "" : CurrentTrack.TrackIdentifier)}/0.jpg")
                            ?.WithTitle(CurrentTrack.Title);
                ControlMessage?.ModifyAsync(properties => properties.Embed = EmbedBuilder.Build());
            }
            return toReturn;
        }

        public override async Task<int> PlayAsync(LavalinkTrack track, bool enqueue, TimeSpan? startTime = null, TimeSpan? endTime = null,
                                                  bool noReplace = false) {
            var initialState = State;
            var toReturn = base.PlayAsync(track, enqueue, startTime, endTime, noReplace);
            var guildConfig = GuildConfig.Get(GuildId);
            if (initialState != PlayerState.Playing && guildConfig.GetChannel(ChannelFunction.Music, out var channel)) {
                EmbedBuilder?.WithAuthor(string.IsNullOrWhiteSpace(track.Author) ? "Unknown" : track.Author,
                                  $"https://img.youtube.com/vi/{(string.IsNullOrWhiteSpace(track.TrackIdentifier) ? "" : track.TrackIdentifier)}/0.jpg")
                            ?.WithThumbnailUrl(
                                  $"https://img.youtube.com/vi/{(string.IsNullOrWhiteSpace(track.TrackIdentifier) ? "" : track.TrackIdentifier)}/0.jpg")
                            ?.WithTitle(track.Title)
                    ?.WithUrl(track.Source);
                BuildEmbedFields();
                ControlMessage = await ((ITextChannel) channel).SendMessageAsync("Debug playback", false, EmbedBuilder.Build());
                UpdateTimer.Start();
                UpdateTimer.Elapsed += (sender, args) => {
                    BuildEmbedFields();
                    ControlMessage.ModifyAsync(properties => properties.Embed = EmbedBuilder.Build());
                };
            }

            return await toReturn;
        }

        private void BuildEmbedFields() {
            EmbedBuilder.Fields.Clear();
            //ProgressBar has 20 segments
            var segments = Convert.ToInt32(TrackPosition.TotalSeconds / (double)CurrentTrack.Duration.TotalSeconds * 20);
            var progressBar = "<==================>";
            if (segments != 0) {
                progressBar = progressBar.Insert(segments, "**");
                progressBar = "**" + progressBar;
            }

            EmbedBuilder.AddField("Playback", progressBar + $" `{TrackPosition:mm':'ss} / {CurrentTrack.Duration:mm':'ss}`", true);
            EmbedBuilder.AddField(Localization.Get(GuildId, "Music.Volume"), $"{Convert.ToInt32(Volume * 100f)}% 🔉", true);
        }

        private Timer UpdateTimer = new Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
        private EmbedBuilder EmbedBuilder = new EmbedBuilder();
        private IUserMessage ControlMessage;

        public void SetControlMessage(IMessage message) { }
    }
}