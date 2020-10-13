using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.Config;
using Bot.Config.Localization.Entries;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Music;
using Bot.DiscordRelated.Music.Tracks;
using Bot.Music;
using Bot.Utilities;
using Bot.Utilities.History;
using Discord.Commands;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using LiteDB;
using Tyrrrz.Extensions;
// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable ConstantConditionalAccessQualifier

#pragma warning disable 4014

namespace Bot.Commands {
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public sealed class MusicCommands : MusicModuleBase {
        [SummonToUser]
        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("play0s")]
        public async Task Play([Remainder] [Summary("play0_0s")] string? query = null) {
            if (!await IsPreconditionsValid)
                return;
            await PlayInternal(query, -1);
        }

        [SummonToUser]
        [Command("playnext", RunMode = RunMode.Async)]
        [Alias("pn")]
        [Summary("playnext0s")]
        public async Task PlayNext([Remainder] [Summary("play0_0s")] string? query = null) {
            if (!await IsPreconditionsValid)
                return;
            await PlayInternal(query, Player.Playlist.Count == 0 ? -1 : Player.CurrentTrackIndex + 1);
        }

        private async Task PlayInternal(string? query, int position) {
            Player.EnqueueControlMessageSend(ResponseChannel);
            var queries = MusicUtils.GetMusicQueries(Context.Message, query.IsEmpty(""));
            if (queries.Count == 0) {
                Context.Message?.SafeDelete();
                return;
            }

            var historyEntry = new HistoryEntry(new EntryLocalized("Music.ResolvingTracks", queries.Count));
            Player.WriteToQueueHistory(historyEntry);

            try {
                var lavalinkTracks = await MusicUtils.LoadMusic(queries);
                Player.TryEnqueue(lavalinkTracks, Context.Message?.Author?.Username ?? "Unknown", position);
            }
            catch (NothingFoundException) {
                ReplyFormattedAsync(Loc.Get("Music.NotFound").Format(query!.SafeSubstring(100, "...")), true).DelayedDelete(Constants.LongTimeSpan);
                if (Player.Playlist.Count == 0) Player.ControlMessage.SafeDelete();
            }
            finally {
                historyEntry.Remove();
            }
        }

        [Command("stop", RunMode = RunMode.Async)]
        [Alias("st")]
        [Summary("stop0s")]
        public async Task Stop() {
            if (!await IsPreconditionsValid) return;

            Player.ExecuteShutdown(Loc.Get("Music.UserStopPlayback").Format(Context.User.Username), new PlayerShutdownParameters{NeedSave = false});
        }

        [Command("jump", RunMode = RunMode.Async)]
        [Alias("j", "skip", "next", "n", "s")]
        [Summary("jump0s")]
        public async Task Jump([Summary("jump0_0s")] int index = 1) {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying".Format(GuildConfig.Prefix))).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            await Player.SkipAsync(index, true);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Jumped", Context.User.Username, Player.CurrentTrackIndex + 1,
                MusicUtils.EscapeTrack(Player.CurrentTrack!.Title).SafeSubstring(0, 40) + "..."));
        }

        [Command("goto", RunMode = RunMode.Async)]
        [Alias("g", "go", "gt")]
        [Summary("goto0s")]
        public async Task Goto([Summary("goto0_0s")] int index) {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            //For programmers who count from 0
            if (index == 0) index = 1;

            if (Player.Playlist.TryGetValue(index - 1, out var track)) {
                await Player.PlayAsync(track!, false);
                Player.WriteToQueueHistory(Loc.Get("MusicQueues.Jumped")
                                              .Format(Context.User.Username, Player.CurrentTrackIndex + 1,
                                                   Player.CurrentTrack!.Title.SafeSubstring(0, 40) + "..."));
            }
            else {
                ReplyFormattedAsync(Loc.Get("Music.TrackIndexWrong").Format(Context.User.Mention, index, Player.Playlist.Count),
                    true).DelayedDelete(Constants.StandardTimeSpan);
            }
        }

        [Command("volume", RunMode = RunMode.Async)]
        [Alias("v")]
        [Summary("volume0s")]
        public async Task Volume([Summary("volume0_0s")] int volume = 100) {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (volume > 150 || volume < 0) {
                ErrorMessageController.AddEntry(Loc.Get("Music.VolumeOutOfRange")).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            await Player.SetVolumeAsync(volume);
            Player.WriteToQueueHistory(new HistoryEntry(
                new EntryLocalized("MusicQueues.NewVolume", Context.User.Username, volume),
                $"{Context.User.Id}volume"));
        }

        [Command("repeat", RunMode = RunMode.Async)]
        [Alias("r", "loop", "l")]
        [Summary("repeat0s")]
        public async Task Repeat(LoopingState state) {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            Player.LoopingState = state;
            Player.UpdateProgress();
            Player.WriteToQueueHistory(new HistoryEntry(
                new EntryLocalized("MusicQueues.RepeatSet", Context.User.Username, Player.LoopingState.ToString()),
                $"{Context.User.Id}repeat"));
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
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (Player.State != PlayerState.Playing) return;

            Player.PauseAsync();
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Pause").Format(Context.User.Username));
        }

        [Command("resume", RunMode = RunMode.Async)]
        [Alias("unpause")]
        [Summary("resume0s")]
        public async Task Resume() {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (Player.State != PlayerState.Paused) return;

            Player.ResumeAsync();
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Resume").Format(Context.User.Username));
        }

        [Command("shuffle", RunMode = RunMode.Async)]
        [Alias("random", "shuf", "shuff", "randomize", "randomise")]
        [Summary("shuffle0s")]
        public async Task Shuffle() {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
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
            if (Player == null || Player.Playlist.IsEmpty) {
                ReplyFormattedAsync(Loc.Get("Music.QueueEmpty").Format(GuildConfig.Prefix), true);
                return;
            }

            Player.PrintQueue(Context.Channel);
        }

        [Hidden]
        [Command("saveplaylist", RunMode = RunMode.Async)]
        [Alias("sp")]
        [Summary("saveplaylist0s")]
        public async Task SavePlaylist() {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            var playlist = Player.ExportPlaylist(ExportPlaylistOptions.IgnoreTrackIndex);
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

            Player.SetControlMessage(logMessage);
            var playlist = GlobalDB.Playlists.FindById(id);
            if (playlist == null) {
                ReplyFormattedAsync(Loc.Get("Music.PlaylistNotFound", id.SafeSubstring(100, "...") ?? ""), true, logMessage);
                return;
            }

            Player.WriteToQueueHistory(Loc.Get("Music.LoadPlaylist", Context.User.Username, id.SafeSubstring(100, "...") ?? ""));
            Player.ImportPlaylist(playlist, options, Context.User.Username);
            Context?.Message?.SafeDelete();
        }

        [SummonToUser]
        [Command("youtube", RunMode = RunMode.Async)]
        [Alias("y", "yt")]
        [Summary("youtube0s")]
        public async Task SearchYoutube([Summary("play0_0s")] [Remainder] string query) {
            if (!await IsPreconditionsValid) return;
            AdvancedMusicSearchChain.CreateInstance(GuildConfig, Player, Context.Channel, Context.User, SearchMode.YouTube, query).Start();
        }

        [SummonToUser]
        [Command("soundcloud", RunMode = RunMode.Async)]
        [Alias("sc")]
        [Summary("soundcloud0s")]
        public async Task SearchSoundCloud([Summary("play0_0s")] [Remainder] string query) {
            if (!await IsPreconditionsValid) return;
            AdvancedMusicSearchChain.CreateInstance(GuildConfig, Player, Context.Channel, Context.User, SearchMode.SoundCloud, query).Start();
        }

        [Command("fastforward", RunMode = RunMode.Async)]
        [Alias("ff", "fwd")]
        [Summary("fastforward0s")]
        public async Task FastForward([Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
            if (!await IsPreconditionsValid) return;
            if (Player?.CurrentTrack == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (!Player.CurrentTrack.IsSeekable) {
                ReplyFormattedAsync(Loc.Get("Music.TrackNotSeekable").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromSeconds(1));
                return;
            }

            var time = timeSpan ?? TimeSpan.FromSeconds(10);
            Player.SeekPositionAsync(Player.TrackPosition + time);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.FF").Format(Context.User.Username, Player.CurrentTrackIndex, time.TotalSeconds));
        }

        [Command("rewind", RunMode = RunMode.Async)]
        [Alias("rw")]
        [Summary("rewind0s")]
        public async Task Rewind([Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
            if (!await IsPreconditionsValid) return;
            if (Player?.CurrentTrack == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (!Player.CurrentTrack.IsSeekable) {
                ReplyFormattedAsync(Loc.Get("Music.TrackNotSeekable").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromSeconds(1));
                return;
            }

            var time = timeSpan ?? new TimeSpan(0, 0, 10);
            Player.SeekPositionAsync(Player.TrackPosition - time);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Rewind").Format(Context.User.Username, Player.CurrentTrackIndex, time.TotalSeconds));
        }

        [Command("seek", RunMode = RunMode.Async)]
        [Alias("sk", "se")]
        [Summary("seek0s")]
        public async Task Seek([Summary("seek0_0s")] TimeSpan position) {
            if (!await IsPreconditionsValid) return;
            if (Player?.CurrentTrack == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (!Player.CurrentTrack.IsSeekable) {
                ReplyFormattedAsync(Loc.Get("Music.TrackNotSeekable").Format(GuildConfig.Prefix), true).DelayedDelete(TimeSpan.FromSeconds(1));
                return;
            }

            Player.SeekPositionAsync(position);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Seek").Format(Context.User.Username, position.FormattedToString()));
        }

        [Command("removerange", RunMode = RunMode.Async)]
        [Alias("rr", "delr", "dr")]
        [Summary("remove0s")]
        public async Task RemoveRange([Summary("remove0_0s")] int start, [Summary("remove0_1s")] int end = -1) {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            start = start.Normalize(1, Player.Playlist.Count);
            end = end.Normalize(start, Player.Playlist.Count);
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
            if (Player == null || Player.Playlist.IsEmpty) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            // For programmers
            if (trackIndex == 0) trackIndex = 1;
            if (trackIndex < 1 || trackIndex > Player.Playlist.Count) {
                ReplyFormattedAsync(Loc.Get("Music.TrackIndexWrong").Format(Context.User.Mention, trackIndex, Player.Playlist.Count),
                    true).DelayedDelete(Constants.ShortTimeSpan);
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
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

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
            Player.SetBassBoostMode(mode);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.BassBoostUpdated").Format(Context.User.Username, mode));
        }

        [Command("changenode", RunMode = RunMode.Async)]
        [Alias("newnode", "switchnode")]
        [Summary("changenode0s")]
        public async Task ChangeNode() {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (MusicUtils.Cluster!.Nodes.Count <= 1) {
                ReplyFormattedAsync(Loc.Get("Music.OnlyOneNode"), true);
                return;
            }

            var currentNode = MusicUtils.Cluster.GetServingNode(Context.Guild.Id);
            var newNode = MusicUtils.Cluster.Nodes.Where(node => node.IsConnected).Where(node => node != currentNode).RandomOrDefault();
            if (newNode == null) {
                ReplyFormattedAsync(Loc.Get("Music.OnlyOneNode"), true);
                return;
            }

            await currentNode.MovePlayerAsync(Player, newNode);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.NodeChanged").Format(Context.User.Username, newNode.Label));
            Player.NodeChanged(newNode);
        }

        [Command("fixspotify", RunMode = RunMode.Async)]
        [Alias("spotify, fs")]
        [Summary("fixspotify0s")]
        public async Task FixSpotify() {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (Player.CurrentTrack is AuthoredTrack authoredTrack && authoredTrack.Track is SpotifyLavalinkTrack spotifyLavalinkTrack) {
                var fixSpotifyChain = FixSpotifyChain.CreateInstance(Context.User, Context.Channel, Loc,
                    $"spotify:track:{spotifyLavalinkTrack.RelatedSpotifyTrack.Id}");
                await fixSpotifyChain.Start();
            }
            else {
                ErrorMessageController.AddEntry(Loc.Get("Music.CurrentTrackNonSpotify")).UpdateTimeout(Constants.StandardTimeSpan).Update();
            }
        }

        [Command("fixspotify", RunMode = RunMode.Async)]
        [Alias("spotify, fs")]
        [Summary("fixspotify0s")]
        public async Task FixSpotify([Remainder] [Summary("fixspotify0_0s")]
                                     string s) {
            var fixSpotifyChain = FixSpotifyChain.CreateInstance(Context.User, Context.Channel, Loc, s);
            await fixSpotifyChain.Start();
        }
    }
}