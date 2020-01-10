using System;
using System.Threading.Tasks;
using Bot.Commands;
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
    [Name("Music")]
    [RequireContext(ContextType.Guild)]
    public sealed class MusicCommands : AdvancedModuleBase
    {
        /// <summary>
        ///     Disconnects from the current voice channel connected to asynchronously.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        [Command("disconnect", RunMode = RunMode.Async)]
        [Summary("Disconnects from the current voice channel connected to asynchronously.")]
        public async Task Disconnect()
        {
            var player = await GetPlayerAsync();

            if (player == null)
            {
                return;
            }

            // when using StopAsync(true) the player also disconnects and clears the track queue.
            // DisconnectAsync only disconnects from the channel.
            await player.StopAsync(true);
            await ReplyAsync("Disconnected.");
        }

        /// <summary>
        ///     Plays music from YouTube asynchronously.
        /// </summary>
        /// <param name="query">the search query</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        [Command("play", RunMode = RunMode.Async)]
        [Summary("Plays music from YouTube")]
        public async Task Play([Remainder]string query)
        {
            var player = await GetPlayerAsync();

            if (player == null)
            {
                return;
            }

            var track = await MusicUtils.Cluster.GetTrackAsync(query, SearchMode.YouTube);

            if (track == null)
            {
                await ReplyAsync("😖 No results.");
                return;
            }

            var position = await player.PlayAsync(track, enqueue: true);

            if (position == 0)
            {
                var message = await ReplyAsync("🔈 Playing: " + track.Source);
                var emote = Emote.Parse("<:GregThinking:660895644864479262>");
                await message.AddReactionAsync(emote);
            }
            else
            {
                await ReplyAsync("🔈 Added to queue: " + track.Source);
            }
        }

        /// <summary>
        ///     Shows the track position asynchronously.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        [Command("position", RunMode = RunMode.Async)]
        [Summary("Shows the track position")]
        public async Task Position()
        {
            var player = await GetPlayerAsync();

            if (player == null)
            {
                return;
            }

            if (player.CurrentTrack == null)
            {
                await ReplyAsync("Nothing playing!");
                return;
            }

            await ReplyAsync($"Position: {player.TrackPosition} / {player.CurrentTrack.Duration}.");
        }

        /// <summary>
        ///     Stops the current track asynchronously.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        [Command("stop", RunMode = RunMode.Async)]
        [Summary("Stops the current track")]
        public async Task Stop()
        {
            var player = await GetPlayerAsync();
            
            if (player == null)
            {
                return;
            }

            if (player.CurrentTrack == null)
            {
                await ReplyAsync("Nothing playing!");
                return;
            }

            await player.StopAsync();
            await ReplyAsync("Stopped playing.");
        }

        /// <summary>
        ///     Updates the player volume asynchronously.
        /// </summary>
        /// <param name="volume">the volume (1 - 1000)</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        [Command("volume", RunMode = RunMode.Async)]
        [Summary("Updates the player volume")]
        public async Task Volume(int volume = 100)
        {
            if (volume > 1000 || volume < 0)
            {
                await ReplyAsync("Volume out of range: 0% - 1000%!");
                return;
            }

            var player = await GetPlayerAsync();
            if (player == null)
            {
                return;
            }
            
            await player.SetVolumeAsync(volume / 100f);
            await ReplyAsync($"Volume updated: {volume}%");
        }

        /// <summary>
        ///     Gets the guild player asynchronously.
        /// </summary>
        /// <param name="connectToVoiceChannel">
        ///     a value indicating whether to connect to a voice channel
        /// </param>
        /// <returns>
        ///     a task that represents the asynchronous operation. The task result is the lavalink player.
        /// </returns>
        private async Task<AdvancedPlayer> GetPlayerAsync(bool connectToVoiceChannel = true)
        {
            var player = MusicUtils.Cluster.GetPlayer<AdvancedPlayer>(Context.Guild.Id);

            if (player != null
                && player.State != PlayerState.NotConnected
                && player.State != PlayerState.Destroyed)
            {
                return player;
            }

            var user = (SocketGuildUser) await Context.Guild.GetUserAsync(Context.User.Id);

            if (!user.VoiceState.HasValue)
            {
                await ReplyAsync("You must be in a voice channel!");
                return null;
            }

            if (connectToVoiceChannel) return await MusicUtils.Cluster.JoinAsync<AdvancedPlayer>(Context.Guild.Id, user.VoiceChannel.Id);
            await ReplyAsync("The bot is not in a voice channel!");
            return null;
        }
    }
}