using System;
using System.Threading.Tasks;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Attributes;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Common;
using Common.History;
using Common.Localization.Entries;
using Common.Music;
using Discord.Commands;
using Lavalink4NET.Players;

// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable ConstantConditionalAccessQualifier

#pragma warning disable 4014

namespace Bot.Commands;

[SlashCommandAdapter]
[Grouping("music")]
[RequireContext(ContextType.Guild)]
public sealed class MusicCommands : HavePlayerMusicModuleBase {
    [Command("stop", RunMode = RunMode.Async)]
    [Alias("st")]
    [Summary("stop0s")]
    public async Task Stop() {
        await Player.Shutdown(new EntryLocalized("Music.UserStopPlayback").WithArg(Context.User.Username),
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
            Player.CurrentTrack!.Title.RemoveNonPrintableChars().SafeSubstring(0, 40) + "..."));
    }

    [RequireNonEmptyPlaylist]
    [Command("goto", RunMode = RunMode.Async)]
    [Alias("g", "go", "gt")]
    [Summary("goto0s")]
    public async Task Goto([Summary("goto0_0s")] int index) {
        //For programmers who count from 0
        if (index == 0) index = 1;
        if (index < 0) {
            // Python like syntax, so -1 is last track
            index = Player.Playlist.Count + index + 1;
        }

        if (Player.Playlist.TryGetValue(index - 1, out var track)) {
            await Player.PlayAsync(track!);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.Jumped")
                .Format(Context.User.Username, Player.CurrentTrackIndex + 1,
                    Player.CurrentTrack!.Title.SafeSubstring(0, 40) + "..."));
        }
        else {
            var description = new EntryLocalized("Music.TrackIndexWrong", Context.User.Mention, index,
                Player.Playlist.Count);
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
    public Task Repeat(LoopingState? state = null) {
        Player.LoopingState = state ?? Player.LoopingState.Next();
        var entryLocalized = new EntryLocalized("MusicQueues.RepeatSet",
            Context.User.Username, Player.LoopingState.ToString());
        Player.WriteToQueueHistory(new HistoryEntry(entryLocalized, $"{Context.User.Id}repeat"));

        return Task.CompletedTask;
    }

    [RequireNonEmptyPlaylist]
    [Command("pause", RunMode = RunMode.Async)]
    [Summary("pause0s")]
    public async Task Pause() {
        if (Player.State != PlayerState.Playing) return;

        await Player.PauseAsync();
        Player.WriteToQueueHistory(Loc.Get("MusicQueues.Pause").Format(Context.User.Username));
    }

    [RequireNonEmptyPlaylist]
    [Command("shuffle", RunMode = RunMode.Async)]
    [Alias("random", "shuf", "shuff", "randomize", "randomise")]
    [Summary("shuffle0s")]
    public Task Shuffle() {
        Player.Playlist.Shuffle();
        Player.WriteToQueueHistory(Loc.Get("MusicQueues.Shuffle").Format(Context.User.Username));
        return Task.CompletedTask;
    }

    [RequireNonEmptyPlaylist]
    [Command("list", RunMode = RunMode.Async)]
    [Alias("l", "q", "queue")]
    [Summary("list0s")]
    public Task List() {
        EmbedPlayerQueueDisplayProvider.CreateOrUpdateQueueDisplay(Context.Channel, Player);
        return Task.CompletedTask;
    }

    [RequireNonEmptyPlaylist(true)]
    [Command("fastforward", RunMode = RunMode.Async)]
    [Alias("ff", "fwd")]
    [Summary("fastforward0s")]
    public async Task FastForward([Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
        if (!Player.CurrentTrack!.IsSeekable) {
            await this.ReplyFailFormattedAsync(new EntryLocalized("Music.TrackNotSeekable", Context.User.Mention), true)
                .CleanupAfter(Constants.ShortTimeSpan);
            return;
        }

        var time = timeSpan ?? TimeSpan.FromSeconds(10);
        await Player.SeekAsync(Player.Position?.Position + time ?? TimeSpan.Zero);
        Player.WriteToQueueHistory(Loc.Get("MusicQueues.FF")
            .Format(Context.User.Username, Player.CurrentTrackIndex + 1, time.TotalSeconds));
    }

    [RequireNonEmptyPlaylist(true)]
    [Command("rewind", RunMode = RunMode.Async)]
    [Alias("rw")]
    [Summary("rewind0s")]
    public async Task Rewind([Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
        if (!Player.CurrentTrack!.IsSeekable) {
            await this.ReplyFailFormattedAsync(new EntryLocalized("Music.TrackNotSeekable", Context.User.Mention), true)
                .CleanupAfter(Constants.ShortTimeSpan);
            return;
        }

        var time = timeSpan ?? new TimeSpan(0, 0, 10);
        await Player.SeekAsync(Player.Position?.Position - time ?? TimeSpan.Zero);
        Player.WriteToQueueHistory(Loc.Get("MusicQueues.Rewind")
            .Format(Context.User.Username, Player.CurrentTrackIndex + 1, time.TotalSeconds));
    }

    [RequireNonEmptyPlaylist(true)]
    [Command("seek", RunMode = RunMode.Async)]
    [Alias("sk", "se")]
    [Summary("seek0s")]
    public async Task Seek([Summary("seek0_0s")] TimeSpan position) {
        if (!Player.CurrentTrack!.IsSeekable) {
            await this.ReplyFailFormattedAsync(new EntryLocalized("Music.TrackNotSeekable", Context.User.Mention), true)
                .CleanupAfter(Constants.ShortTimeSpan);
            return;
        }

        await Player.SeekAsync(position);
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
                deletedTrack.Track.Title.RemoveNonPrintableChars().SafeSubstring(30)));
        }
        else {
            Player.Playlist.RemoveRange(start - 1, countToRemove);
            Player.WriteToQueueHistory(Loc.Get("MusicQueues.RemoveRange", Context.User.Username, countToRemove, start,
                end));
        }

        if (Player.CurrentTrackIndex == -1 && Player.Playlist.Count != 0) {
            var track = Player.Playlist[Math.Min(start - 1, Player.Playlist.Count)];
            await Player.PlayAsync(track);
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
            var description = new EntryLocalized("Music.TrackIndexWrong", Context.User.Mention, trackIndex,
                Player.Playlist.Count);
            await this.ReplyFailFormattedAsync(description, true).CleanupAfter(Constants.ShortTimeSpan);
        }

        newIndex = Math.Max(1, Math.Min(Player.Playlist.Count, newIndex));
        Player.Playlist.Move(trackIndex - 1, newIndex - 1);
        Player.WriteToQueueHistory(Loc.Get("MusicQueues.TrackMoved")
            .Format(Context.User.Mention, trackIndex, newIndex));
    }

    [Command("playerrestart", RunMode = RunMode.Async)]
    [Summary("playerrestart0s")]
    [CommandCooldown(GuildDelayMilliseconds = 60000)]
    public async Task RestartPlayer() {
        var playerShutdownParameters = new PlayerShutdownParameters()
            { ShutdownDisplays = false, SavePlaylist = false, RestartPlayer = true };
        await Player.Shutdown(playerShutdownParameters);
    }
}