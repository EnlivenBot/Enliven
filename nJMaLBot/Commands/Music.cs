using System;
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
using LiteDB;

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
                ReplyFormattedAsync(Loc.Get("Music.NotFound").Format(query.SafeSubstring(0, 512)), true).DelayedDelete(TimeSpan.FromMinutes(10));
                if (player.Playlist.Count == 0) player.ControlMessage.SafeDelete();
            }
            catch (AttachmentAddFailException) {
                ReplyFormattedAsync(Loc.Get("Music.AttachmentFail"), true, logMessage).DelayedDelete(TimeSpan.FromMinutes(10));
                if (player.Playlist.Count == 0) player.ControlMessage.SafeDelete();
            }
        }

        [Command("stop", RunMode = RunMode.Async)]
        [Alias("s")]
        [Summary("stop0s")]
        public async Task Stop() {
            var player = await GetPlayerAsync();
            if (player?.CurrentTrack == null) {
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                Context.Message.SafeDelete();
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
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
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
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                Context.Message.SafeDelete();
                return;
            }

            //For programmers who count from 0
            if (index == 0) index = 1;

            if (player.Playlist.TryGetValue(index - 1, out var track)) {
                await player.PlayAsync(track, false);
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
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                Context.Message.SafeDelete();
                return;
            }

            if (volume > 150 || volume < 0) {
                ReplyFormattedAsync(Loc.Get("Music.VolumeOutOfRange"), true).DelayedDelete(TimeSpan.FromMinutes(1));
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
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
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
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
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
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
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
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
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
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
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

            void PrintList(StringBuilder builder, IUserMessage message = null) {
                builder.Append("```");
                MusicUtils.EscapeTrack(builder);
                ReplyFormattedAsync(builder.ToString(), false, message);
            }

            Context.Message.SafeDelete();
        }

        [Hidden]
        [Command("saveplaylist", RunMode = RunMode.Async)]
        [Alias("sp")]
        [Summary("saveplaylist0s")]
        public async Task SavePlaylist() {
            var player = await GetPlayerAsync();
            if (player == null || player.Playlist.Count == 0) {
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                ;
                Context.Message.SafeDelete();
                return;
            }

            var playlist = player.GetExportPlaylist(ExportPlaylistOptions.IgnoreTrackIndex);
            var storedPlaylist = new StoredPlaylist {
                Tracks = playlist.Tracks, TrackIndex = playlist.TrackIndex, TrackPosition = playlist.TrackPosition,
                Id = "u" + ObjectId.NewObjectId()
            };
            GlobalDB.Playlists.Insert(storedPlaylist);
            ReplyFormattedAsync(Loc.Get("Music.PlaylistSaved").Format(storedPlaylist.Id.ToString(), GuildConfig.Prefix));
            Context.Message.SafeDelete();
        }

        [Command("loadplaylist", RunMode = RunMode.Async)]
        [Alias("lp")]
        [Summary("loadplaylist0s")]
        public async Task LoadPlaylist([Summary("playlistId")] [Remainder] string id) {
            await ExecutePlaylist(id, ImportPlaylistOptions.Replace);
        }

        [Command("addplaylist", RunMode = RunMode.Async)]
        [Alias("ap")]
        [Summary("addplaylist0s")]
        public async Task AddPlaylist([Summary("playlistId")] [Remainder] string id) {
            await ExecutePlaylist(id, ImportPlaylistOptions.JustAdd);
        }

        [Command("runplaylist", RunMode = RunMode.Async)]
        [Alias("rp")]
        [Summary("runplaylist0s")]
        public async Task RunPlaylist([Summary("playlistId")] [Remainder] string id) {
            await ExecutePlaylist(id, ImportPlaylistOptions.AddAndPlay);
        }

        private async Task ExecutePlaylist(string id, ImportPlaylistOptions options) {
            var player = await GetPlayerAsync(true);
            var logMessage = await GetLogMessage();
            if (logMessage == null || player == null) {
                logMessage.SafeDelete();
                return;
            }

            player.SetControlMessage(logMessage);
            var playlist = GlobalDB.Playlists.FindById(id);
            if (playlist == null) {
                ReplyFormattedAsync(Loc.Get("PlaylistNotFound").Format(id.SafeSubstring(0, 40)), true, logMessage);
                return;
            }

            player.WriteToQueueHistory(Loc.Get("Music.LoadPlaylist").Format(Context.User.Username, id.SafeSubstring(0, 40)));
            player.ImportPlaylist(playlist, options, Context.User.Username);
            Context?.Message?.SafeDelete();
        }
    }
}