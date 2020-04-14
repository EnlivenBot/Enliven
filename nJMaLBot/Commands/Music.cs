using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Music;
using Bot.Music.Players;
using Bot.Utilities;
using Bot.Utilities.Collector;
using Bot.Utilities.Commands;
using Bot.Utilities.Modules;
using Discord;
using Discord.Commands;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
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
            if (!await IsPreconditionsValid)
                return;
            var logMessage = GetLogMessage();

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
        [Alias("st")]
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
        [Alias("j", "skip", "next", "n", "s")]
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
        [Alias("r", "loop", "l")]
        [Summary("repeat0s")]
        public async Task Repeat(LoopingState state) {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ReplyFormattedAsync(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromMinutes(2));
                return;
            }

            Player.LoopingState = state;
            Player.UpdateProgress();
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
            var logMessage = await GetLogMessage();
            if (Player == null || Player.Playlist.IsEmpty) {
                ReplyFormattedAsync(Loc.Get("Music.QueueEmpty").Format(GuildConfig.Prefix), true, logMessage);
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
                    await PrintList(queue, logMessage);
                    logMessage = null;
                    queue = new StringBuilder("```py\n");
                }

                queue.Append(builder);
            }

            await PrintList(queue, logMessage);

            async Task PrintList(StringBuilder builder, IUserMessage message = null) {
                builder.Append("```");
                MusicUtils.EscapeTrack(builder);
                await ReplyFormattedAsync(builder.ToString(), false, message);
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
                ReplyFormattedAsync(Loc.Get("Music.PlaylistNotFound").Format(id.SafeSubstring(0, 40)), true, logMessage);
                return;
            }

            Player.WriteToQueueHistory(Loc.Get("Music.LoadPlaylist").Format(Context.User.Username, id.SafeSubstring(0, 40)));
            Player.ImportPlaylist(playlist, options, Context.User.Username);
            Context?.Message?.SafeDelete();
        }

        [SummonToUser]
        [Command("youtube", RunMode = RunMode.Async)]
        [Alias("y", "yt")]
        [Summary("youtube0s")]
        public async Task SearchYoutube([Summary("play0_0s")] [Remainder] string query) {
            await AdvancedSearch(SearchMode.YouTube, query);
        }

        [SummonToUser]
        [Command("soundcloud", RunMode = RunMode.Async)]
        [Alias("sc")]
        [Summary("soundcloud0s")]
        public async Task SearchSoundCloud([Summary("play0_0s")] [Remainder] string query) {
            await AdvancedSearch(SearchMode.SoundCloud, query);
        }

        private async Task AdvancedSearch(SearchMode mode, string query) {
            if (!await IsPreconditionsValid) return;
            var tracks = (await MusicUtils.Cluster.GetTracksAsync(query, mode)).ToList();
            var eb = new EmbedBuilder().WithColor(Color.Gold).WithTitle(Loc.Get("Music.SearchResultsTitle"))
                                       .WithDescription(Loc.Get("Music.SearchResultsDescription").Format(mode, query.SafeSubstring(0, 40)));
            if (!tracks.Any()) {
                eb.Description += Loc.Get("Music.NothingFound");
            }
            else {
                var builder = new StringBuilder();
                for (var i = 0; i < tracks.Count && builder.Length < 1500 && i < 10; i++) {
                    var track = tracks[i];
                    builder.AppendLine($"{i + 1}. [{track.Title}]({track.Source})\n");
                }

                eb.Description += builder.ToString();
            }

            var msg = await ReplyAsync(null, false, eb.Build());
            if (!tracks.Any())
                return;

            var controller = CollectorsUtils.CollectReaction(msg, reaction => true, async args => {
                args.RemoveReason();
                var i = args.Reaction.Emote.Name switch {
                    "1️⃣" => 0,
                    "2️⃣" => 1,
                    "3️⃣" => 2,
                    "4️⃣" => 3,
                    "5️⃣" => 4,
                    "6️⃣" => 5,
                    "7️⃣" => 6,
                    "8️⃣" => 7,
                    "9️⃣" => 8,
                    "🔟"  => 9,
                    "⬅️"  => -2,
                    _     => -1
                };

                var authoredLavalinkTracks = new List<AuthoredLavalinkTrack>();
                if (i == -2)
                    authoredLavalinkTracks.AddRange(tracks.Take(10).Select(track => AuthoredLavalinkTrack.FromLavalinkTrack(track, Context.User.Username)));
                if (i >= 0 && i <= tracks.Count - 1) authoredLavalinkTracks.Add(AuthoredLavalinkTrack.FromLavalinkTrack(tracks[i], Context.User.Username));
                switch (authoredLavalinkTracks.Count) {
                    case 0:
                        return;
                    case 1:
                        Player.WriteToQueueHistory(Loc.Get("MusicQueues.Enqueued")
                                                      .Format(Context.Message.Author.Username, MusicUtils.EscapeTrack(authoredLavalinkTracks[0].Title)));
                        break;
                    default:
                        Player.WriteToQueueHistory(Loc.Get("Music.AddTracks").Format(Context.Message.Author.Username, authoredLavalinkTracks.Count));
                        break;
                }

                var logMessage = await GetLogMessage();
                await Player.SetControlMessage(logMessage);
                await Player.PlayAsync(authoredLavalinkTracks.First(), true);
                Player.Playlist.AddRange(authoredLavalinkTracks.Skip(1));

                args.Controller.Dispose();
            }, CollectorFilter.IgnoreBots);
            controller.SetTimeout(TimeSpan.FromSeconds(30));
            controller.Stop += (sender, args) => msg.SafeDelete();
            msg.AddReactionsAsync(new IEmote[] {
                new Emoji("1️⃣"),
                new Emoji("2️⃣"),
                new Emoji("3️⃣"),
                new Emoji("4️⃣"),
                new Emoji("5️⃣"),
                new Emoji("6️⃣"),
                new Emoji("7️⃣"),
                new Emoji("8️⃣"),
                new Emoji("9️⃣"),
                new Emoji("🔟"),
                new Emoji("⬅️")
            });
        }

        [Command("fastforward", RunMode = RunMode.Async)]
        [Alias("fast forward", "ff", "fwd")]
        [Summary("fastforward0s")]
        public async Task FastForward([Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
            if (!await IsPreconditionsValid) return;
            var time = timeSpan ?? new TimeSpan(0, 0, 10);
            Player.SeekPositionAsync(Player.TrackPosition + time);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.FF").Format(Context.User.Username, Player.CurrentTrackIndex, time.TotalSeconds));
        }

        [Command("rewind", RunMode = RunMode.Async)]
        [Alias("rw")]
        [Summary("rewind0s")]
        public async Task Rewind([Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
            if (!await IsPreconditionsValid) return;
            var time = timeSpan ?? new TimeSpan(0, 0, 10);
            Player.SeekPositionAsync(Player.TrackPosition - time);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Rewind").Format(Context.User.Username, Player.CurrentTrackIndex, time.TotalSeconds));
        }

        [Command("seek", RunMode = RunMode.Async)]
        [Alias("sk", "se")]
        [Summary("seek0s")]
        public async Task Seek([Summary("seek0_0s")] TimeSpan position) {
            if (!await IsPreconditionsValid) return;
            Player.SeekPositionAsync(position);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Seek").Format(Context.User.Username, Player.CurrentTrackIndex, position));
        }

        [Command("removerange", RunMode = RunMode.Async)]
        [Alias("rr", "delr", "dr")]
        [Summary("remove0s")]
        public async Task RemoveRange([Summary("remove0_0s")] int start, [Summary("remove0_1s")] int end = -1) {
            if (!await IsPreconditionsValid) return;
            start = Math.Max(1, Math.Min(start, Player.Playlist.Count));
            end = Math.Max(start, Math.Min(end, Player.Playlist.Count));
            var removesCurrent = Player.CurrentTrackIndex + 1 >= start && Player.CurrentTrackIndex + 1 <= end;
            Player.Playlist.RemoveRange(start - 1, end - start + 1);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Remove").Format(Context.User.Username, end - start + 1, start, end));
            if (removesCurrent) {
                Jump();
            }
        }

        [Command("remove", RunMode = RunMode.Async)]
        [Alias("rm", "del", "delete")]
        [Summary("remove0s")]
        public async Task Remove([Summary("remove0_0s")] int start, [Summary("remove1_1s")] int count = 1) {
            await RemoveRange(start, start + count - 1);
        }

        [Command("move", RunMode = RunMode.Async)]
        [Alias("m", "mv")]
        [Summary("move0s")]
        public async Task Move([Summary("move0_0s")] int trackIndex, [Summary("move0_1s")] int newIndex = 1) {
            if (!await IsPreconditionsValid) return;

            // For programmers
            if (trackIndex == 0) trackIndex = 1;
            if (trackIndex < 1 || trackIndex > Player.Playlist.Count) {
                ReplyFormattedAsync(Loc.Get("Music.TrackIndexWrong").Format(Context.User.Mention, trackIndex, Player.Playlist.Count),
                    true).DelayedDelete(TimeSpan.FromMinutes(2));
            }

            newIndex = Math.Max(1, Math.Min(Player.Playlist.Count, newIndex));
            Player.Playlist.Move(trackIndex - 1, newIndex - 1);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.TrackMoved").Format(Context.User.Mention, trackIndex, newIndex));
        }

        [Command("bassboost", RunMode = RunMode.Async)]
        [Alias("bb", "bassboosted")]
        [Summary("bassboost0s")]
        public async Task ApplyBassBoost([Summary("bassboost0_0s")] BassBoostMode mode) {
            if (!await IsPreconditionsValid) return;
            var bands = new List<EqualizerBand>();
            switch (mode) {
                case BassBoostMode.Off:
                    bands.Add(new EqualizerBand(0, 0f));
                    bands.Add(new EqualizerBand(1, 0f));
                    break;
                case BassBoostMode.Low:
                    bands.Add(new EqualizerBand(0, 0.25f));
                    bands.Add(new EqualizerBand(1, 0.15f));
                    break;
                case BassBoostMode.Medium:
                    bands.Add(new EqualizerBand(0, 0.5f));
                    bands.Add(new EqualizerBand(1, 0.25f));
                    break;
                case BassBoostMode.High:
                    bands.Add(new EqualizerBand(0, 0.75f));
                    bands.Add(new EqualizerBand(1, 0.5f));
                    break;
                case BassBoostMode.Extreme:
                    bands.Add(new EqualizerBand(0, 1f));
                    bands.Add(new EqualizerBand(1, 0.75f));
                    break;
            }

            await Player.UpdateEqualizerAsync(bands, false);
            Player.BassBoostMode = mode;
            Player.UpdateParameters();
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.BassBoostUpdated").Format(Context.User.Username, mode));
        }
    }
}