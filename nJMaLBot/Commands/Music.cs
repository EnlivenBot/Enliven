using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Music;
using Bot.Music.Players;
using Bot.Utilities;
using Bot.Utilities.Commands;
using Bot.Utilities.Modules;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET.Player;
using Embed = Discord.Embed;

#pragma warning disable 4014

namespace Bot.Commands {
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public sealed class MusicCommands : MusicModuleBase {
        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("play0s")]
        public async Task Play([Remainder] [Summary("play0_0s")] string query = null) {
            var player = await GetPlayerAsync(true);
            var logMessage = await GetLogMessage();
            if (logMessage == null || player == null) {
                logMessage.SafeDelete();
                return;
            }
            
            player.SetControlMessage(logMessage);
            try {
                await MusicUtils.QueueLoadMusic(Context.Message, query, player);
            }
            catch (EmptyQueryException) {
                Context.Message?.SafeDelete();
            }
            catch (NothingFoundException) {
                ReplyFormattedAsync(Loc.Get("Music.NotFound").Format(query.SafeSubstring(0, 512)), true);
                if (player.Playlist.Count == 0) player.ControlMessage.SafeDelete();
            }
            catch (AttachmentAddFailException) {
                ReplyFormattedAsync(Loc.Get("Music.AttachmentFail"), true, logMessage);
                if (player.Playlist.Count == 0) player.ControlMessage.SafeDelete();
            }
        }

        [Command("stop", RunMode = RunMode.Async)]
        [Alias("s")]
        [Summary("stop0s")]
        public async Task Stop() {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            if (player.CurrentTrack == null) {
                await ReplyFormattedAsync(Loc.Get("Music.NothingPlaying"), true);
                return;
            }

            player.PrepareShutdown(Loc.Get("Music.UserStopPlayback").Format(Context.User.Username));
            await player.StopAsync(true);
            Context.Message.SafeDelete();
        }

        [Command("jump", RunMode = RunMode.Async)]
        [Alias("j", "skip", "next", "n")]
        [Summary("jump0s")]
        public async Task Jump([Summary("jump0_0s")] int index = 1) {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            await player.SkipAsync(index, true);
            player.WriteToQueueHistory(Loc.Get("MusicQueues.Jumped")
                                          .Format(Context.User.Username, player.CurrentTrackIndex + 1,
                                               player.CurrentTrack.Title.SafeSubstring(0, 40) + "..."));
            Context.Message.SafeDelete();
        }

        [Command("goto", RunMode = RunMode.Async)]
        [Alias("g")]
        [Summary("goto0s")]
        public async Task Goto([Summary("goto0_0s")] int index) {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            //For programmers who count from 0
            if (index == 0) index = 1;

            if (player.Playlist.TryGetValue(index - 1, out var track)) {
                await player.PlayAsync(track, false, new TimeSpan?(), new TimeSpan?());
                player.WriteToQueueHistory(Loc.Get("MusicQueues.Jumped")
                                              .Format(Context.User.Username, player.CurrentTrackIndex + 1,
                                                   player.CurrentTrack.Title.SafeSubstring(0, 40) + "..."));
            }
            else {
                ReplyFormattedAsync(Loc.Get("Music.TrackIndexWrong").Format(Context.User.Mention, index, player.Playlist.Count),
                        true).DelayedDelete(TimeSpan.FromMinutes(5));
            }

            Context.Message.SafeDelete();
        }

        [Command("volume", RunMode = RunMode.Async)]
        [Alias("v")]
        [Summary("volume0s")]
        public async Task Volume([Summary("volume0_0s")] int volume = 100) {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            if (volume > 150 || volume < 0) {
                await ReplyFormattedAsync(Loc.Get("Music.VolumeOutOfRange"), true);
                return;
            }

            await player.SetVolumeAsync(volume / 100f);
            Context.Message.SafeDelete();
            player.WriteToQueueHistory(Loc.Get("MusicQueues.NewVolume").Format(Context.User.Username, volume));
        }

        [Command("repeat", RunMode = RunMode.Async)]
        [Alias("r")]
        [Summary("repeat0s")]
        public async Task Repeat(LoopingState state) {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            player.LoopingState = state;
            Context.Message.SafeDelete();
            player.WriteToQueueHistory(Loc.Get("MusicQueues.RepeatSet").Format(Context.User.Username, player.LoopingState.ToString()));
        }

        [Command("repeat", RunMode = RunMode.Async)]
        [Alias("r", "loop", "l")]
        [Summary("repeat0s")]
        public async Task Repeat() {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            Repeat(player.LoopingState.Next());
        }

        [Command("pause", RunMode = RunMode.Async)]
        [Summary("pause0s")]
        public async Task Pause() {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            Context.Message.SafeDelete();
            player.PauseAsync();
            player.WriteToQueueHistory(Loc.Get("MusicQueues.Pause").Format(Context.User.Username));
        }

        [Command("resume", RunMode = RunMode.Async)]
        [Alias("unpause")]
        [Summary("resume0s")]
        public async Task Resume() {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            Context.Message.SafeDelete();
            player.ResumeAsync();
            player.WriteToQueueHistory(Loc.Get("MusicQueues.Resume").Format(Context.User.Username));
        }

        [Command("shuffle", RunMode = RunMode.Async)]
        [Alias("random", "shuf", "shuff", "randomize", "randomise")]
        [Summary("shuffle0s")]
        public async Task Shuffle() {
            var player = await GetPlayerAsync();
            if (player == null) {
                Context.Message.SafeDelete();
                return;
            }

            Context.Message.SafeDelete();
            player.Playlist.Shuffle();
            player.WriteToQueueHistory(Loc.Get("MusicQueues.Shuffle").Format(Context.User.Username));
        }

        [Command("list", RunMode = RunMode.Async)]
        [Alias("l", "q", "queue")]
        [Summary("list0s")]
        public async Task List() {
            var player = await GetPlayerAsync();
            var logMessage = await GetLogMessage();
            if (player == null || player.Playlist.IsEmpty) {
                Context.Message.SafeDelete();
                ReplyFormattedAsync(Loc.Get("Music.QueueEmpty").Format(GuildConfig.Prefix), true, logMessage);
                return;
            }

            var queue = new StringBuilder("```py\n");
            for (var index = 0; index < player.Playlist.Count; index++) {
                var builder = new StringBuilder();
                var lavalinkTrack = player.Playlist[index];
                builder.Append(player.CurrentTrackIndex == index ? "@" : " ");
                builder.Append(index + 1);
                builder.Append(": ");
                builder.AppendLine(lavalinkTrack.Title);
                if (queue.Length + builder.Length > 2000) {
                    PrintList(queue, logMessage);
                    logMessage = null;
                    queue = new StringBuilder("```py\n");
                }

                queue.Append(builder);
            }

            PrintList(queue, logMessage);

            Context.Message.SafeDelete();
        }

        private async Task PrintList(StringBuilder builder, IUserMessage message = null) {
            builder.Append("```");
            MusicUtils.EscapeTrack(builder);
            ReplyFormattedAsync(builder.ToString(), false, message);
        }
    }
}