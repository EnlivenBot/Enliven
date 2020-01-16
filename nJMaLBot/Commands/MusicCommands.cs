using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Commands;
using Bot.Utilities;
using Bot.Utilities.Commands;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;

namespace Bot.Music {
    /// <summary>
    ///     Presents some of the main features of the Lavalink4NET-Library.
    /// </summary>
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public sealed class MusicCommands : AdvancedModuleBase {
        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("play1s")]
        public async Task PlayFromAttachment() {
            var player = await GetPlayerAsync();
            if (player == null) return;

            var sb = new StringBuilder();
            if (Context.Message.Attachments.Count != 0) {
                foreach (var attachment in Context.Message.Attachments) {
                    var lavalinkTrack = await MusicUtils.Cluster.GetTrackAsync(attachment.Url);

                    if (lavalinkTrack == null) {
                        sb.AppendLine(Loc.Get("Music.AttachmentFail").Format(attachment.Filename.SafeSubstring(0, 20)));
                    }
                    else {
                        await player.PlayAsync(lavalinkTrack, enqueue: true);
                        sb.AppendLine(Loc.Get("Music.AttachmentAdd").Format(attachment.Filename.SafeSubstring(0, 20)));
                    }
                }
            }

            #pragma warning disable 4014
            ReplyAsync(sb.ToString());
            #pragma warning restore 4014
            Context.Message.SafeDelete();
        }

        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("play0s")]
        public async Task Play([Remainder] [Summary("play0_0s")] string query) {
            var player = await GetPlayerAsync();
            if (player == null) return;

            var tracks = (await MusicUtils.Cluster.GetTracksAsync(query, SearchMode.YouTube)).ToList();
            if (!tracks.Any()) {
                (await ReplyAsync("😖 No results.")).DelayedDelete(TimeSpan.FromMinutes(10));
                return;
            }

            tracks = MusicUtils.IsValidUrl(query) ? tracks : new List<LavalinkTrack> {tracks.First()};



            var position = await player.PlayAsync(tracks.First(), enqueue: true);
            player.Queue.AddRange(tracks.Skip(1));

            if (position == 0) {
                (await ReplyAsync($"🔈 Playing: {tracks[0].Source}" + (tracks.Count == 1 ? "" : $" Enqueued `{tracks.Count}` tracks"))).DelayedDelete(TimeSpan.FromMinutes(5));
            }
            else {
                (await ReplyAsync($"🔈 {tracks.Count} added to queue.")).DelayedDelete(TimeSpan.FromMinutes(5));
            }

            Context.Message.SafeDelete();
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
        public async Task Volume([Summary("volume0_0s")]int volume = 100) {
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
        
        private async Task<AdvancedPlayer> GetPlayerAsync(bool connectToVoiceChannel = true) {
            var player = MusicUtils.Cluster.GetPlayer<AdvancedPlayer>(Context.Guild.Id);

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

            if (connectToVoiceChannel) return await MusicUtils.Cluster.JoinAsync<AdvancedPlayer>(Context.Guild.Id, user.VoiceChannel.Id);
            await ReplyAsync("The bot is not in a voice channel!");
            return null;
        }
    }
}