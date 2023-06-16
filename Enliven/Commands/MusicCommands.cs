using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Attributes;
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


        [Command("stop", RunMode = RunMode.Async)]
        [Alias("st")]
        [Summary("stop0s")]
        public async Task Stop() {
            Player.Shutdown(Loc.Get("Music.UserStopPlayback").Format(Context.User.Username),
                new PlayerShutdownParameters { SavePlaylist = false, ShutdownDisplays = true });
        }

        [RequireNonEmptyPlaylist]
        [Command("jump", RunMode = RunMode.Async)]
        [Alias("j", "skip", "next", "n", "s", "jmp")]
        [Summary("jump0s")]
        public async Task Jump([Summary("jump0_0s")] int index = 1) {
            await Player.SkipAsync(index, true);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Jumped", Context.User.Username,
                Player.CurrentTrackIndex + 1,
                Common.Music.Controller.MusicController.EscapeTrack(Player.CurrentTrack!.Title).SafeSubstring(0, 40) + "..."));
        }

        [RequireNonEmptyPlaylist]
        [Command("goto", RunMode = RunMode.Async)]
        [Alias("g", "go", "gt")]
        [Summary("goto0s")]
        public async Task Goto([Summary("goto0_0s")] int index) {
            //For programmers who count from 0
            if (index == 0) index = 1;

            if (Player.Playlist.TryGetValue(index - 1, out var track)) {
                await Player.PlayAsync(track!, false);
                Player.WriteToQueueHistory(Loc.Get("MusicQueues.Jumped")
                    .Format(Context.User.Username, Player.CurrentTrackIndex + 1,
                        Player.CurrentTrack!.Title.SafeSubstring(0, 40) + "..."));
            }
            else {
                var description = new EntryLocalized("Music.TrackIndexWrong", Context.User.Mention, index, Player.Playlist.Count);
                await this.ReplyFailFormattedAsync(description, true).CleanupAfter(Constants.ShortTimeSpan);
            }
        }

        [Command("volume", RunMode = RunMode.Async)]
        [Alias("v")]
        [Summary("volume0s")]
        public async Task Volume([Summary("volume0_0s")] int volume = 100) {
            if (volume is > 200 or < 10) {
                await this.ReplyFailFormattedAsync(new EntryLocalized("Music.VolumeOutOfRange"), true);
                return;
            }

            await Player.SetVolumeAsync(volume);
            var entryLocalized = new EntryLocalized("MusicQueues.NewVolume", Context.User.Username, volume);
            Player.WriteToQueueHistory(new HistoryEntry(entryLocalized, $"{Context.User.Id}volume"));
        }

        [Command("repeat", RunMode = RunMode.Async)]
        [Alias("r", "loop", "l")]
        [Summary("repeat0s")]
        public async Task Repeat(LoopingState? state = null) {
            Player.LoopingState = state ?? Player.LoopingState.Next();
            // Player.UpdateProgress();
            var entryLocalized = new EntryLocalized("MusicQueues.RepeatSet", Context.User.Username, Player.LoopingState.ToString());
            Player.WriteToQueueHistory(new HistoryEntry(entryLocalized, $"{Context.User.Id}repeat"));
        }

        [RequireNonEmptyPlaylist]
        [Command("pause", RunMode = RunMode.Async)]
        [Summary("pause0s")]
        public async Task Pause() {
            if (Player.State != PlayerState.Playing) return;

            Player.PauseAsync();
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Pause").Format(Context.User.Username));
        }

        [ShouldCreatePlayer]
        [Command("resume", RunMode = RunMode.Async)]
        [Alias("unpause")]
        [Summary("resume0s")]
        public async Task Resume() {
            if (Player.Playlist.IsEmpty) {
                var playerSnapshot = MusicController.GetPlayerLastSnapshot(Context.Guild.Id);
                if (playerSnapshot == null) {
                    await this.ReplyFailFormattedAsync(new EntryLocalized("Music.NoSnapshotFoundToResume"), true);
                    return;
                }
                await Player.ApplyStateSnapshot(playerSnapshot);
                Player.WriteToQueueHistory(new EntryLocalized("Music.PlayerRestored", Context.User.Username));
                return;
            }

            if (Player.State != PlayerState.Paused) return;

            Player.ResumeAsync();
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Resume").Format(Context.User.Username));
        }

        [RequireNonEmptyPlaylist]
        [Command("shuffle", RunMode = RunMode.Async)]
        [Alias("random", "shuf", "shuff", "randomize", "randomise")]
        [Summary("shuffle0s")]
        public async Task Shuffle() {
            Player.Playlist.Shuffle();
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Shuffle").Format(Context.User.Username));
        }

        [RequireNonEmptyPlaylist]
        [Command("list", RunMode = RunMode.Async)]
        [Alias("l", "q", "queue")]
        [Summary("list0s")]
        public async Task List() {
            EmbedPlayerQueueDisplayProvider.CreateOrUpdateQueueDisplay(Context.Channel, Player);
        }

        [ShouldCreatePlayer]
        [Command("youtube", RunMode = RunMode.Async)]
        [Alias("y", "yt")]
        [Summary("youtube0s")]
        public async Task SearchYoutube([Summary("play0_0s")] [Remainder] string query) {
            new AdvancedMusicSearchChain(GuildConfig, Player!, Context.Channel, Context.User, SearchMode.YouTube, query, MusicController, ComponentService, CollectorService).Start();
        }

        [ShouldCreatePlayer]
        [Command("soundcloud", RunMode = RunMode.Async)]
        [Alias("sc")]
        [Summary("soundcloud0s")]
        public async Task SearchSoundCloud([Summary("play0_0s")] [Remainder] string query) {
            new AdvancedMusicSearchChain(GuildConfig, Player!, Context.Channel, Context.User, SearchMode.SoundCloud, query, MusicController, ComponentService, CollectorService).Start();
        }

        [RequireNonEmptyPlaylist(true)]
        [Command("fastforward", RunMode = RunMode.Async)]
        [Alias("ff", "fwd")]
        [Summary("fastforward0s")]
        public async Task FastForward([Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
            if (!Player.CurrentTrack!.IsSeekable) {
                await this.ReplyFailFormattedAsync(new EntryLocalized("Music.TrackNotSeekable", Context.User.Mention), true).CleanupAfter(Constants.ShortTimeSpan);
                return;
            }

            var time = timeSpan ?? TimeSpan.FromSeconds(10);
            Player.SeekPositionAsync(Player.TrackPosition + time);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.FF")
                .Format(Context.User.Username, Player.CurrentTrackIndex, time.TotalSeconds));
        }

        [RequireNonEmptyPlaylist(true)]
        [Command("rewind", RunMode = RunMode.Async)]
        [Alias("rw")]
        [Summary("rewind0s")]
        public async Task Rewind([Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
            if (!Player.CurrentTrack!.IsSeekable) {
                await this.ReplyFailFormattedAsync(new EntryLocalized("Music.TrackNotSeekable", Context.User.Mention), true).CleanupAfter(Constants.ShortTimeSpan);
                return;
            }

            var time = timeSpan ?? new TimeSpan(0, 0, 10);
            Player.SeekPositionAsync(Player.TrackPosition - time);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Rewind")
                .Format(Context.User.Username, Player.CurrentTrackIndex, time.TotalSeconds));
        }

        [RequireNonEmptyPlaylist(true)]
        [Command("seek", RunMode = RunMode.Async)]
        [Alias("sk", "se")]
        [Summary("seek0s")]
        public async Task Seek([Summary("seek0_0s")] TimeSpan position) {
            if (!Player.CurrentTrack!.IsSeekable) {
                await this.ReplyFailFormattedAsync(new EntryLocalized("Music.TrackNotSeekable", Context.User.Mention), true).CleanupAfter(Constants.ShortTimeSpan);
                return;
            }

            Player.SeekPositionAsync(position);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Seek")
                .Format(Context.User.Username, position.FormattedToString()));
        }

        [RequireNonEmptyPlaylist]
        [Command("removerange", RunMode = RunMode.Async)]
        [Alias("rr", "delr", "dr")]
        [Summary("remove0s")]
        public async Task RemoveRange([Summary("remove0_0s")] int start, [Summary("remove0_1s")] int end = -1) {
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

        [RequireNonEmptyPlaylist]
        [Command("remove", RunMode = RunMode.Async)]
        [Alias("rm", "del", "delete")]
        [Summary("remove0s")]
        public async Task Remove([Summary("remove0_0s")] int start, [Summary("remove1_1s")] int count = 1) {
            await RemoveRange(start, start + count - 1);
        }

        [RequireNonEmptyPlaylist]
        [Command("move", RunMode = RunMode.Async)]
        [Alias("m", "mv")]
        [Summary("move0s")]
        public async Task Move([Summary("move0_0s")] int trackIndex, [Summary("move0_1s")] int newIndex = 1) {
            // For programmers
            if (trackIndex == 0) trackIndex = 1;
            if (trackIndex < 1 || trackIndex > Player.Playlist.Count) {
                var description = new EntryLocalized("Music.TrackIndexWrong", Context.User.Mention, trackIndex, Player.Playlist.Count);
                await this.ReplyFailFormattedAsync(description, true).CleanupAfter(Constants.ShortTimeSpan);
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
            var cluster = await MusicController.ClusterTask;
            if (cluster.Nodes.Count <= 1) {
                await this.ReplyFailFormattedAsync(new EntryLocalized("Music.OnlyOneNode"), true);
                return;
            }

            var currentNode = cluster.GetServingNode(Context.Guild.Id);
            var newNode = cluster.Nodes
                .Where(node => node.IsConnected)
                .Where(node => node != currentNode)
                .RandomOrDefault();

            if (newNode == null) {
                await this.ReplyFailFormattedAsync(new EntryLocalized("Music.OnlyOneNode"), true);
                return;
            }

            await currentNode.MovePlayerAsync(Player, newNode);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.NodeChanged").Format(Context.User.Username, newNode.Label));
        }

        [Command("playerrestart", RunMode = RunMode.Async)]
        [Summary("playerrestart0s")]
        [CommandCooldown(GuildDelayMilliseconds = 60000)]
        public async Task RestartPlayer() {
            var playerShutdownParameters = new PlayerShutdownParameters() { ShutdownDisplays = false, SavePlaylist = false };
            await Player.Shutdown(playerShutdownParameters);
            await Task.Delay(1000);
            
            var playerSnapshot = MusicController.GetPlayerLastSnapshot(Context.Guild.Id)!;
            var newPlayer = await MusicController.ProvidePlayer(Context.Guild.Id, playerSnapshot.LastVoiceChannelId, true);
            await newPlayer.ApplyStateSnapshot(playerSnapshot);
            foreach (var playerDisplay in Player.Displays.ToList()) 
                await playerDisplay.ChangePlayer(newPlayer);
            newPlayer.WriteToQueueHistory(new EntryLocalized("Music.PlayerRestored", Context.User.Username));
        }

        private static Regex _lyricsRegex = new Regex(@"([\p{L} ]+) - ([\p{L} ]+)");

        [RequireNonEmptyPlaylist(true)]
        [Command("lyrics", RunMode = RunMode.Async)]
        [Summary("lyrics0s")]
        public async Task DisplayLyrics() {
            var match = _lyricsRegex.Match(Player.CurrentTrack!.Title);
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

            if (string.IsNullOrWhiteSpace(lyrics)) {
                await this.ReplyFailFormattedAsync(new EntryLocalized("Music.LyricsNotFound", artist, title), true);
                return;
            }
            await this.ReplyFormattedAsync(title.ToEntry(), lyrics.ToEntry());
        }
    }
}