using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Music;
using Bot.Music.Players;
using Bot.Utilities;
using Bot.Utilities.Commands;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;

#pragma warning disable 4014

namespace Bot.Commands {
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public sealed class MusicCommands : AdvancedModuleBase {
        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("play0s")]
        public async Task Play([Remainder] [Summary("play0_0s")] string query = null) {
            var message = ReplyAsync(Loc.Get("Music.Loading"));
            var player = await GetPlayerAsync();
            if (player == null) return;

            var lavalinkTracks = new List<LavalinkTrack>();
            if (Context.Message.Attachments.Count != 0) {
                var lavalinkTrack = await MusicUtils.Cluster.GetTrackAsync(Context.Message.Attachments.First().Url);
                if (lavalinkTrack != null) {
                    lavalinkTracks.Add(lavalinkTrack);
                }
                else {
                    (await message).ModifyAsync(properties => {
                        properties.Content = "";
                        properties.Embed = new EmbedBuilder().WithTitle(Loc.Get("Music.Fail")).WithDescription(Loc.Get("Music.AttachmentFail")).Build();
                    });
                    return;
                }
            }
            else if (MusicUtils.IsValidUrl(query)) {
                lavalinkTracks.AddRange(await MusicUtils.Cluster.GetTracksAsync(query, SearchMode.YouTube));
            }
            else {
                var track = await MusicUtils.Cluster.GetTrackAsync(query, SearchMode.YouTube);
                if (track != null) {
                    lavalinkTracks.Add(track);
                }
            }
            
            if (!lavalinkTracks.Any()) {
                (await message).ModifyAsync(properties => {
                    properties.Content = "";
                    properties.Embed = new EmbedBuilder().WithTitle(Loc.Get("Music.Fail")).WithDescription(Loc.Get("Music.NotFound")).Build();
                });
                return;
            }
            
            var tracks = lavalinkTracks.Select(track => AuthoredLavalinkTrack.FromLavalinkTrack(track, Context.User)).ToList();
            player.SetControlMessage(await message);
            await player.PlayAsync(tracks.First(), true);
            player.Playlist.AddRange(tracks.Skip(1));
        }

        [Command("stop", RunMode = RunMode.Async)]
        [Alias("s")]
        [Summary("stop0s")]
        public async Task Stop() {
            var player = await GetPlayerAsync();

            if (player == null) {
                return;
            }

            if (player.CurrentTrack == null) {
                await ReplyAsync("Nothing playing!");
                return;
            }

            await player.StopAsync(true);
            await ReplyAsync("Stopped playing.");
        }

        [Command("volume", RunMode = RunMode.Async)]
        [Alias("v")]
        [Summary("volume0s")]
        public async Task Volume([Summary("volume0_0s")] int volume = 100) {
            if (volume > 1000 || volume < 0) {
                await ReplyAsync("Volume out of range: 0% - 1000%!");
                return;
            }

            var player = await GetPlayerAsync();
            if (player == null) {
                return;
            }

            await player.SetVolumeAsync(volume / 100f);
            await ReplyAsync($"Volume updated: {volume}%");
        }

        private async Task<EmbedPlaybackPlayer> GetPlayerAsync(bool connectToVoiceChannel = true) {
            var player = MusicUtils.Cluster.GetPlayer<EmbedPlaybackPlayer>(Context.Guild.Id);

            if (player != null
             && player.State != PlayerState.NotConnected
             && player.State != PlayerState.Destroyed) {
                return player;
            }

            var user = (SocketGuildUser) await Context.Guild.GetUserAsync(Context.User.Id);

            if (!user.VoiceState.HasValue) {
                await ReplyAsync("You must be in a voice channel!");
                return null;
            }

            if (connectToVoiceChannel) return await MusicUtils.Cluster.JoinAsync<EmbedPlaybackPlayer>(Context.Guild.Id, user.VoiceChannel.Id);
            await ReplyAsync("The bot is not in a voice channel!");
            return null;
        }
    }
}