using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.MessageComponents;
using Bot.Utilities.Collector;
using Common;
using Common.History;
using Common.Localization.Entries;
using Common.Music;
using Common.Music.Players;
using Discord;
using Discord.Commands;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Tyrrrz.Extensions;

// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable ConstantConditionalAccessQualifier

#pragma warning disable 4014

namespace Bot.Commands {
    [SlashCommandAdapter]
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public sealed class MusicCommands : MusicModuleBase {
        public MessageComponentService ComponentService { get; set; } = null!;
        public CollectorService CollectorService { get; set; } = null!;

        [SummonToUser]
        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("play0s")]
        public async Task Play([Remainder] [Summary("play0_0s")] string? query = null) {
            if (!await IsPreconditionsValid)
                return;
            await PlayInternal(query);
        }

        [SummonToUser]
        [Command("playnext", RunMode = RunMode.Async)]
        [Alias("pn")]
        [Summary("playnext0s")]
        public async Task PlayNext([Remainder] [Summary("play0_0s")] string? query = null) {
            if (!await IsPreconditionsValid)
                return;
            await PlayInternal(query, Player!.Playlist.Count == 0 ? -1 : Player.CurrentTrackIndex + 1);
        }

        private async Task PlayInternal(string? query, int position = -1) {
            var queries = Common.Music.Controller.MusicController.GetMusicQueries(Context.Message, query.IsBlank(""));
            if (queries.Count == 0) {
                Context.Message?.SafeDelete();
                if (MainDisplay != null) MainDisplay.NextResendForced = true;
                return;
            }

            MainDisplay?.ControlMessageResend();
            try {
                await Player!.TryEnqueue(await MusicController.ResolveQueries(queries), Context.User?.Username ?? "Unknown", position);
            }
            catch (TrackNotFoundException) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NotFound", query!.SafeSubstring(100, "...")!))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
            }
        }

        [Command("stop", RunMode = RunMode.Async)]
        [Alias("st")]
        [Summary("stop0s")]
        public async Task Stop() {
            if (!await IsPreconditionsValid) return;

            Player.Shutdown(Loc.Get("Music.UserStopPlayback").Format(Context.User.Username),
                new PlayerShutdownParameters {SavePlaylist = false, ShutdownDisplays = true});
        }

        [Command("jump", RunMode = RunMode.Async)]
        [Alias("j", "skip", "next", "n", "s", "jmp")]
        [Summary("jump0s")]
        public async Task Jump([Summary("jump0_0s")] int index = 1) {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying".Format(GuildConfig.Prefix)))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            await Player.SkipAsync(index, true);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Jumped", Context.User.Username,
                Player.CurrentTrackIndex + 1,
                Common.Music.Controller.MusicController.EscapeTrack(Player.CurrentTrack!.Title).SafeSubstring(0, 40) + "..."));
        }

        [Command("goto", RunMode = RunMode.Async)]
        [Alias("g", "go", "gt")]
        [Summary("goto0s")]
        public async Task Goto([Summary("goto0_0s")] int index) {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
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
                var description = Loc.Get("Music.TrackIndexWrong").Format(Context.User.Mention, index, Player.Playlist.Count);
                _ = ReplyFormattedAsync(description, true).DelayedDelete(Constants.ShortTimeSpan);
            }
        }

        [Command("volume", RunMode = RunMode.Async)]
        [Alias("v")]
        [Summary("volume0s")]
        public async Task Volume([Summary("volume0_0s")] int volume = 100) {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (volume > 200 || volume < 10) {
                ErrorMessageController.AddEntry(Loc.Get("Music.VolumeOutOfRange"))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
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
        public async Task Repeat(LoopingState? state) {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            Player.LoopingState = state ?? Player!.LoopingState.Next();
            // Player.UpdateProgress();
            Player.WriteToQueueHistory(new HistoryEntry(
                new EntryLocalized("MusicQueues.RepeatSet", Context.User.Username, Player.LoopingState.ToString()),
                $"{Context.User.Id}repeat"));
        }

        [Command("pause", RunMode = RunMode.Async)]
        [Summary("pause0s")]
        public async Task Pause() {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
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
                await RestorePlayer();

                return;
            }

            if (Player.State != PlayerState.Paused) return;

            Player.ResumeAsync();
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Resume").Format(Context.User.Username));
        }

        private async Task RestorePlayer() {
            var newPlayer = await MusicController.RestoreLastPlayer(Context.Guild.Id);
            if (newPlayer == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
            }
            else {
                GetChannel(out var musicChannel);
                MainDisplay = EmbedPlayerDisplayProvider.Provide((ITextChannel) musicChannel, newPlayer);
                newPlayer.WriteToQueueHistory(new EntryLocalized("Music.PlayerRestored", Context.User.Username));
            }
        }

        [Command("shuffle", RunMode = RunMode.Async)]
        [Alias("random", "shuf", "shuff", "randomize", "randomise")]
        [Summary("shuffle0s")]
        public async Task Shuffle() {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
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

            EmbedPlayerQueueDisplayProvider.CreateOrUpdateQueueDisplay(Context.Channel, Player);
        }

        [SummonToUser]
        [Command("youtube", RunMode = RunMode.Async)]
        [Alias("y", "yt")]
        [Summary("youtube0s")]
        public async Task SearchYoutube([Summary("play0_0s")] [Remainder] string query) {
            if (!await IsPreconditionsValid) return;
            new AdvancedMusicSearchChain(GuildConfig, Player!, Context.Channel, Context.User, SearchMode.YouTube, query, MusicController, ComponentService, CollectorService).Start();
        }

        [SummonToUser]
        [Command("soundcloud", RunMode = RunMode.Async)]
        [Alias("sc")]
        [Summary("soundcloud0s")]
        public async Task SearchSoundCloud([Summary("play0_0s")] [Remainder] string query) {
            if (!await IsPreconditionsValid) return;
            new AdvancedMusicSearchChain(GuildConfig, Player!, Context.Channel, Context.User, SearchMode.SoundCloud, query, MusicController, ComponentService, CollectorService).Start();
        }

        [Command("fastforward", RunMode = RunMode.Async)]
        [Alias("ff", "fwd")]
        [Summary("fastforward0s")]
        public async Task FastForward([Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
            if (!await IsPreconditionsValid) return;
            if (Player?.CurrentTrack == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (!Player.CurrentTrack.IsSeekable) {
                _ = ReplyFormattedAsync(Loc.Get("Music.TrackNotSeekable").Format(GuildConfig.Prefix), true).DelayedDelete(Constants.ShortTimeSpan);
                return;
            }

            var time = timeSpan ?? TimeSpan.FromSeconds(10);
            Player.SeekPositionAsync(Player.TrackPosition + time);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.FF")
                                          .Format(Context.User.Username, Player.CurrentTrackIndex, time.TotalSeconds));
        }

        [Command("rewind", RunMode = RunMode.Async)]
        [Alias("rw")]
        [Summary("rewind0s")]
        public async Task Rewind([Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
            if (!await IsPreconditionsValid) return;
            if (Player?.CurrentTrack == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (!Player.CurrentTrack.IsSeekable) {
                _ = ReplyFormattedAsync(Loc.Get("Music.TrackNotSeekable").Format(GuildConfig.Prefix), true).DelayedDelete(Constants.ShortTimeSpan);
                return;
            }

            var time = timeSpan ?? new TimeSpan(0, 0, 10);
            Player.SeekPositionAsync(Player.TrackPosition - time);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Rewind")
                                          .Format(Context.User.Username, Player.CurrentTrackIndex, time.TotalSeconds));
        }

        [Command("seek", RunMode = RunMode.Async)]
        [Alias("sk", "se")]
        [Summary("seek0s")]
        public async Task Seek([Summary("seek0_0s")] TimeSpan position) {
            if (!await IsPreconditionsValid) return;
            if (Player?.CurrentTrack == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (!Player.CurrentTrack.IsSeekable) {
                _ = ReplyFormattedAsync(Loc.Get("Music.TrackNotSeekable").Format(GuildConfig.Prefix), true).DelayedDelete(Constants.ShortTimeSpan);
                return;
            }

            Player.SeekPositionAsync(position);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Seek")
                                          .Format(Context.User.Username, position.FormattedToString()));
        }

        [Command("removerange", RunMode = RunMode.Async)]
        [Alias("rr", "delr", "dr")]
        [Summary("remove0s")]
        public async Task RemoveRange([Summary("remove0_0s")] int start, [Summary("remove0_1s")] int end = -1) {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            start = start.Normalize(1, Player.Playlist.Count);
            end = end.Normalize(start, Player.Playlist.Count);
            var countToRemove = end - start + 1;
            if (countToRemove == 1) {
                var deletedTrack = Player.Playlist[start - 1];
                Player.Playlist.RemoveRange(start - 1, countToRemove);
                Player.WriteToQueueHistory(Loc.Get("MusicQueues.Remove", Context.User.Username, start,
                    Common.Music.Controller.MusicController.EscapeTrack(deletedTrack.Title.SafeSubstring(30)!)));
            }
            else {
                Player.Playlist.RemoveRange(start - 1, countToRemove);
                Player.WriteToQueueHistory(Loc.Get("MusicQueues.RemoveRange", Context.User.Username, countToRemove, start, end));
            }

            if (Player.CurrentTrackIndex == -1 && Player.Playlist.Count != 0) {
                var track = Player.Playlist[Math.Min(start - 1, Player.Playlist.Count)];
                Player.PlayAsync(track, false);
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
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            // For programmers
            if (trackIndex == 0) trackIndex = 1;
            if (trackIndex < 1 || trackIndex > Player.Playlist.Count) {
                var description = Loc.Get("Music.TrackIndexWrong").Format(Context.User.Mention, trackIndex, Player.Playlist.Count);
                _ = ReplyFormattedAsync(description, true).DelayedDelete(Constants.ShortTimeSpan);
            }

            newIndex = Math.Max(1, Math.Min(Player.Playlist.Count, newIndex));
            Player.Playlist.Move(trackIndex - 1, newIndex - 1);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.TrackMoved").Format(Context.User.Mention, trackIndex, newIndex));
        }

        [Command("changenode", RunMode = RunMode.Async)]
        [Alias("newnode", "switchnode")]
        [Summary("changenode0s")]
        [CommandCooldown(GuildDelayMilliseconds = 5000)]
        public async Task ChangeNode() {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (MusicController.Cluster.Nodes.Count <= 1) {
                ReplyFormattedAsync(Loc.Get("Music.OnlyOneNode"), true);
                return;
            }

            var currentNode = MusicController.Cluster.GetServingNode(Context.Guild.Id);
            var newNode = MusicController.Cluster.Nodes.Where(node => node.IsConnected)
                                         .Where(node => node != currentNode).RandomOrDefault();
            if (newNode == null) {
                ReplyFormattedAsync(Loc.Get("Music.OnlyOneNode"), true);
                return;
            }

            await currentNode.MovePlayerAsync(Player, newNode);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.NodeChanged").Format(Context.User.Username, newNode.Label));
        }

        [Command("playerrestart", RunMode = RunMode.Async)]
        [Summary("playerrestart0s")]
        [CommandCooldown(GuildDelayMilliseconds = 60000)]
        public async Task RestartPlayer() {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            var playerShutdownParameters = new PlayerShutdownParameters() {ShutdownDisplays = true, SavePlaylist = false};
            Player.Shutdown(playerShutdownParameters);
            Player = null;
            await Task.Delay(1000);
            await RestorePlayer();
        }

        private static Regex _lyricsRegex = new Regex(@"([\p{L} ]+) - ([\p{L} ]+)");

        [Command("lyrics", RunMode = RunMode.Async)]
        [Summary("lyrics0s")]
        public async Task DisplayLyrics() {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty || Player.CurrentTrack == null) {
                ErrorMessageController.AddEntry(Loc.Get("Music.NothingPlaying").Format(GuildConfig.Prefix))
                                      .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            var match = _lyricsRegex.Match(Player.CurrentTrack.Title);
            string? lyrics = null;
            string artist = "";
            string title = "";
            if (match.Success) {
                artist = match.Groups[1].Value.Trim();
                title = match.Groups[2].Value.Trim();
                lyrics = await LyricsService.GetLyricsAsync(artist, title);
            }

            if (string.IsNullOrWhiteSpace(lyrics)) {
                artist = Player.CurrentTrack.Author;
                title = Player.CurrentTrack.Title;
                lyrics = await LyricsService.GetLyricsAsync(artist, title);
            }

            var isFail = string.IsNullOrWhiteSpace(lyrics);
            await ReplyFormattedAsync((isFail ? Loc.Get("Music.LyricsNotFound", artist, title) : lyrics)!, isFail);
        }
    }
}