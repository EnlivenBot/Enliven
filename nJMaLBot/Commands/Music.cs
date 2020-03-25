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
        [SummonToUser]
        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("play0s")]
        public async Task Play([Remainder] [Summary("play0_0s")] string query = null) {
            var logMessage = GetLogMessage();
            if (!await IsPreconditionsValid) {
                (await logMessage).SafeDelete();
                return;
            }

            Player.SetControlMessage(await logMessage);
            try {
                await MusicUtils.QueueLoadMusic(Context.Message, query, Player);
            }
            catch (EmptyQueryException) {
                Context.Message?.SafeDelete();
            }
            catch (NothingFoundException) {
                ReplyFormattedAsync(Loc.Get("Music.NotFound").Format(query.SafeSubstring(0, 512)), true).DelayedDelete(TimeSpan.FromMinutes(10));
                if (Player.Playlist.Count == 0) Player.ControlMessage.SafeDelete();
            }
            catch (AttachmentAddFailException) {
                ReplyFormattedAsync(Loc.Get("Music.AttachmentFail"), true, await logMessage).DelayedDelete(TimeSpan.FromMinutes(10));
                if (Player.Playlist.Count == 0) Player.ControlMessage.SafeDelete();
            }
        }

        [Command("stop", RunMode = RunMode.Async)]
        [Alias("s")]
        [Summary("stop0s")]
        public async Task Stop() {
            if (!await IsPreconditionsValid) return;
            if (Player?.CurrentTrack == null) {
                    ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                return;
            }

            Player.PrepareShutdown(Loc.Get("Music.UserStopPlayback").Format(Context.User.Username));
            await Player.StopAsync(true);
        }

        [Command("jump", RunMode = RunMode.Async)]
        [Alias("j", "skip", "next", "n")]
        [Summary("jump0s")]
        public async Task Jump([Summary("jump0_0s")] int index = 1) {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                    ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                return;
            }

            await Player.SkipAsync(index, true);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Jumped")
                                          .Format(Context.User.Username, Player.CurrentTrackIndex + 1,
                                               Player.CurrentTrack.Title.SafeSubstring(0, 40) + "..."));
        }

        [Command("goto", RunMode = RunMode.Async)]
        [Alias("g")]
        [Summary("goto0s")]
        public async Task Goto([Summary("goto0_0s")] int index) {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                    ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                return;
            }

            //For programmers who count from 0
            if (index == 0) index = 1;

            if (Player.Playlist.TryGetValue(index - 1, out var track)) {
                await Player.PlayAsync(track, false);
                Player.WriteToQueueHistory(Loc.Get("MusicQueues.Jumped")
                                              .Format(Context.User.Username, Player.CurrentTrackIndex + 1,
                                                   Player.CurrentTrack.Title.SafeSubstring(0, 40) + "..."));
            }
            else {
                ReplyFormattedAsync(Loc.Get("Music.TrackIndexWrong").Format(Context.User.Mention, index, Player.Playlist.Count),
                    true).DelayedDelete(TimeSpan.FromMinutes(5));
            }
        }

        [Command("volume", RunMode = RunMode.Async)]
        [Alias("v")]
        [Summary("volume0s")]
        public async Task Volume([Summary("volume0_0s")] int volume = 100) {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                    ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                return;
            }

            if (volume > 150 || volume < 0) {
                ReplyFormattedAsync(Loc.Get("Music.VolumeOutOfRange"), true).DelayedDelete(TimeSpan.FromMinutes(1));
                return;
            }

            await Player.SetVolumeAsync(volume / 100f);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.NewVolume").Format(Context.User.Username, volume));
        }

        [Command("repeat", RunMode = RunMode.Async)]
        [Alias("r")]
        [Summary("repeat0s")]
        public async Task Repeat(LoopingState state) {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                    ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                return;
            }

            Player.LoopingState = state;
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.RepeatSet").Format(Context.User.Username, Player.LoopingState.ToString()));
        }

        [Command("repeat", RunMode = RunMode.Async)]
        [Alias("r", "loop", "l")]
        [Summary("repeat0s")]
        public async Task Repeat() {
            await Repeat(Player.LoopingState.Next());
        }

        [Command("pause", RunMode = RunMode.Async)]
        [Summary("pause0s")]
        public async Task Pause() {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                    ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                return;
            }

            Player.PauseAsync();
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Pause").Format(Context.User.Username));
        }

        [Command("resume", RunMode = RunMode.Async)]
        [Alias("unpause")]
        [Summary("resume0s")]
        public async Task Resume() {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                    ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                return;
            }

            Player.ResumeAsync();
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Resume").Format(Context.User.Username));
        }

        [Command("shuffle", RunMode = RunMode.Async)]
        [Alias("random", "shuf", "shuff", "randomize", "randomise")]
        [Summary("shuffle0s")]
        public async Task Shuffle() {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                    ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                return;
            }

            Player.Playlist.Shuffle();
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Shuffle").Format(Context.User.Username));
        }

        [Command("list", RunMode = RunMode.Async)]
        [Alias("l", "q", "queue")]
        [Summary("list0s")]
        public async Task List() {
            if (!await IsPreconditionsValid) return;
            var logMessage = GetLogMessage();
            if (Player == null || Player.Playlist.IsEmpty) {
                ReplyFormattedAsync(Loc.Get("Music.QueueEmpty").Format(GuildConfig.Prefix), true, await logMessage);
                return;
            }

            var queue = new StringBuilder("```py\n");
            for (var index = 0; index < Player.Playlist.Count; index++) {
                var builder = new StringBuilder();
                var lavalinkTrack = Player.Playlist[index];
                builder.Append(Player.CurrentTrackIndex == index ? "@" : " ");
                builder.Append(index + 1);
                builder.Append(": ");
                builder.AppendLine(lavalinkTrack.Title);
                if (queue.Length + builder.Length > 2000) {
                    PrintList(queue, await logMessage);
                    logMessage = null;
                    queue = new StringBuilder("```py\n");
                }

                queue.Append(builder);
            }

            PrintList(queue, await logMessage);

            void PrintList(StringBuilder builder, IUserMessage message = null) {
                builder.Append("```");
                MusicUtils.EscapeTrack(builder);
                ReplyFormattedAsync(builder.ToString(), false, message);
            }
        }

        [Hidden]
        [Command("saveplaylist", RunMode = RunMode.Async)]
        [Alias("sp")]
        [Summary("saveplaylist0s")]
        public async Task SavePlaylist() {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.Count == 0) {
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                return;
            }

            var playlist = Player.GetExportPlaylist(ExportPlaylistOptions.IgnoreTrackIndex);
            var storedPlaylist = new StoredPlaylist {
                Tracks = playlist.Tracks, TrackIndex = playlist.TrackIndex, TrackPosition = playlist.TrackPosition,
                Id = "u" + ObjectId.NewObjectId()
            };
            GlobalDB.Playlists.Insert(storedPlaylist);
            ReplyFormattedAsync(Loc.Get("Music.PlaylistSaved").Format(storedPlaylist.Id.ToString(), GuildConfig.Prefix));
        }

        [SummonToUser]
        [Command("loadplaylist", RunMode = RunMode.Async)]
        [Alias("lp")]
        [Summary("loadplaylist0s")]
        public async Task LoadPlaylist([Summary("playlistId")] [Remainder] string id) {
            await ExecutePlaylist(id, ImportPlaylistOptions.Replace);
        }

        [SummonToUser]
        [Command("addplaylist", RunMode = RunMode.Async)]
        [Alias("ap")]
        [Summary("addplaylist0s")]
        public async Task AddPlaylist([Summary("playlistId")] [Remainder] string id) {
            await ExecutePlaylist(id, ImportPlaylistOptions.JustAdd);
        }

        [SummonToUser]
        [Command("runplaylist", RunMode = RunMode.Async)]
        [Alias("rp")]
        [Summary("runplaylist0s")]
        public async Task RunPlaylist([Summary("playlistId")] [Remainder] string id) {
            await ExecutePlaylist(id, ImportPlaylistOptions.AddAndPlay);
        }

        private async Task ExecutePlaylist(string id, ImportPlaylistOptions options) {
            if (!await IsPreconditionsValid) return;
            var logMessage = await GetLogMessage();
            if (logMessage == null || Player == null) {
                logMessage.SafeDelete();
                return;
            }

            Player.SetControlMessage(logMessage);
            var playlist = GlobalDB.Playlists.FindById(id);
            if (playlist == null) {
                ReplyFormattedAsync(Loc.Get("PlaylistNotFound").Format(id.SafeSubstring(0, 40)), true, logMessage);
                return;
            }

            Player.WriteToQueueHistory(Loc.Get("Music.LoadPlaylist").Format(Context.User.Username, id.SafeSubstring(0, 40)));
            Player.ImportPlaylist(playlist, options, Context.User.Username);
            Context?.Message?.SafeDelete();
        }
    }
}