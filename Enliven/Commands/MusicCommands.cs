using System;
using System.Linq;
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
using Discord.Interactions;
using Lavalink4NET.Cluster.Nodes;
using Lavalink4NET.Players;
using ContextType = Discord.Commands.ContextType;
using RunMode = Discord.Commands.RunMode;

// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable ConstantConditionalAccessQualifier

#pragma warning disable 4014

namespace Bot.Commands;

[SlashCommandAdapter]
[Grouping("music")]
[Discord.Commands.RequireContext(ContextType.Guild)]
public sealed class MusicCommands : HavePlayerMusicModuleBase {
    [Command("stop", RunMode = RunMode.Async)]
    [Alias("st")]
    [Discord.Commands.Summary("stop0s")]
    public async Task Stop() {
        await Player.Shutdown(new EntryLocalized("Music.UserStopPlayback").WithArg(Context.User.Mention),
            new PlayerShutdownParameters { SavePlaylist = false, ShutdownDisplays = true });
    }

    [RequireNonEmptyPlaylist]
    [Command("jump", RunMode = RunMode.Async)]
    [Alias("j", "skip", "next", "n", "s", "jmp")]
    [Discord.Commands.Summary("jump0s")]
    public async Task Jump([Discord.Commands.Summary("jump0_0s")] int index = 1) {
        await Player.SkipAsync(index, true);
        Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.Jumped", Context.User.Mention,
            Player.RequestedTrackIndex + 1,
            Player.CurrentTrack!.Title.RemoveNonPrintableChars().SafeSubstring(0, 40) + "..."));
    }

    [RequireNonEmptyPlaylist]
    [Command("goto", RunMode = RunMode.Async)]
    [Alias("g", "go", "gt")]
    [Discord.Commands.Summary("goto0s")]
    public async Task Goto([Discord.Commands.Summary("goto0_0s")] int index) {
        //For programmers who count from 0
        if (index == 0) index = 1;
        if (index < 0) {
            // Python like syntax, so -1 is last track
            index = Player.Playlist.Count + index + 1;
        }

        if (Player.Playlist.TryGetValue(index - 1, out var track)) {
            await Player.PlayAsync(track!);
            Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.Jumped", Context.User.Mention,
                Player.RequestedTrackIndex + 1,
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
    [Discord.Commands.Summary("volume0s")]
    public async Task Volume([Discord.Commands.Summary("volume0_0s")] int volume = 100) {
        if (volume is > 200 or < 10) {
            await this.ReplyFailFormattedAsync(new EntryLocalized("Music.VolumeOutOfRange"), true);
            return;
        }

        await Player.SetVolumeAsync(volume);
        var entryLocalized = new EntryLocalized("PlayerHistory.NewVolume", Context.User.Mention, volume);
        Player.WriteToQueueHistory(new HistoryEntry(entryLocalized, $"{Context.User.Id}volume"));
    }

    [Command("repeat", RunMode = RunMode.Async)]
    [Alias("r", "loop", "l")]
    [Discord.Commands.Summary("repeat0s")]
    public Task Repeat(LoopingState? state = null) {
        Player.LoopingState = state ?? Player.LoopingState.Next();
        var entryLocalized = new EntryLocalized("PlayerHistory.RepeatSet",
            Context.User.Mention, Player.LoopingState.ToString());
        Player.WriteToQueueHistory(new HistoryEntry(entryLocalized, $"{Context.User.Id}repeat"));

        return Task.CompletedTask;
    }

    [RequireNonEmptyPlaylist]
    [Command("pause", RunMode = RunMode.Async)]
    [Discord.Commands.Summary("pause0s")]
    public async Task Pause() {
        if (Player.State != PlayerState.Playing) return;

        await Player.PauseAsync();
        Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.Pause", Context.User.Mention));
    }

    [RequireNonEmptyPlaylist]
    [Command("shuffle", RunMode = RunMode.Async)]
    [Alias("random", "shuf", "shuff", "randomize", "randomise")]
    [Discord.Commands.Summary("shuffle0s")]
    public Task Shuffle() {
        Player.Playlist.Shuffle();
        Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.Shuffle", Context.User.Mention));
        return Task.CompletedTask;
    }

    [RequireNonEmptyPlaylist]
    [Command("list", RunMode = RunMode.Async)]
    [Alias("l", "q", "queue")]
    [Discord.Commands.Summary("list0s")]
    public Task List() {
        EmbedPlayerQueueDisplayProvider.CreateOrUpdateQueueDisplay(Context.Channel, Player);
        return Task.CompletedTask;
    }

    [RequireNonEmptyPlaylist(true)]
    [Command("fastforward", RunMode = RunMode.Async)]
    [Alias("ff", "fwd")]
    [Discord.Commands.Summary("fastforward0s")]
    public async Task FastForward([Discord.Commands.Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
        if (!Player.CurrentTrack!.IsSeekable) {
            await this.ReplyFailFormattedAsync(new EntryLocalized("Music.TrackNotSeekable", Context.User.Mention), true)
                .CleanupAfter(Constants.ShortTimeSpan);
            return;
        }

        var time = timeSpan ?? TimeSpan.FromSeconds(10);
        await Player.SeekAsync(Player.Position?.Position + time ?? TimeSpan.Zero);
        Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.FF", Context.User.Mention,
            Player.RequestedTrackIndex + 1, time.TotalSeconds));
    }

    [RequireNonEmptyPlaylist(true)]
    [Command("rewind", RunMode = RunMode.Async)]
    [Alias("rw")]
    [Discord.Commands.Summary("rewind0s")]
    public async Task Rewind([Discord.Commands.Summary("fastforward0_0s")] TimeSpan? timeSpan = null) {
        if (!Player.CurrentTrack!.IsSeekable) {
            await this.ReplyFailFormattedAsync(new EntryLocalized("Music.TrackNotSeekable", Context.User.Mention), true)
                .CleanupAfter(Constants.ShortTimeSpan);
            return;
        }

        var time = timeSpan ?? new TimeSpan(0, 0, 10);
        await Player.SeekAsync(Player.Position?.Position - time ?? TimeSpan.Zero);
        Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.Rewind", Context.User.Mention,
            Player.RequestedTrackIndex + 1, time.TotalSeconds));
    }

    [RequireNonEmptyPlaylist(true)]
    [Command("seek", RunMode = RunMode.Async)]
    [Alias("sk", "se")]
    [Discord.Commands.Summary("seek0s")]
    public async Task Seek([Discord.Commands.Summary("seek0_0s")] TimeSpan position) {
        if (!Player.CurrentTrack!.IsSeekable) {
            await this.ReplyFailFormattedAsync(new EntryLocalized("Music.TrackNotSeekable", Context.User.Mention), true)
                .CleanupAfter(Constants.ShortTimeSpan);
            return;
        }

        await Player.SeekAsync(position);
        Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.Seek", Context.User.Mention,
            position.FormattedToString()));
    }

    [RequireNonEmptyPlaylist]
    [Command("removerange", RunMode = RunMode.Async)]
    [Alias("rr", "delr", "dr")]
    [Discord.Commands.Summary("remove0s")]
    public async Task RemoveRange([Discord.Commands.Summary("remove0_0s")] int start,
        [Discord.Commands.Summary("remove0_1s")] int end = -1) {
        start = start.Normalize(1, Player.Playlist.Count);
        end = end.Normalize(start, Player.Playlist.Count);
        var countToRemove = end - start + 1;
        if (countToRemove == 1) {
            var deletedTrack = Player.Playlist[start - 1];
            Player.Playlist.RemoveRange(start - 1, countToRemove);
            Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.Remove", Context.User.Mention, start,
                deletedTrack.Track.Title.RemoveNonPrintableChars().SafeSubstring(30)));
        }
        else {
            Player.Playlist.RemoveRange(start - 1, countToRemove);
            Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.RemoveRange", Context.User.Mention,
                countToRemove, start,
                end));
        }

        if (Player.RequestedTrackIndex == -1 && Player.Playlist.Count != 0) {
            var track = Player.Playlist[Math.Min(start - 1, Player.Playlist.Count)];
            await Player.PlayAsync(track);
        }
    }

    [RequireNonEmptyPlaylist]
    [Command("remove", RunMode = RunMode.Async)]
    [Alias("rm", "del", "delete")]
    [Discord.Commands.Summary("remove0s")]
    public async Task Remove([Discord.Commands.Summary("remove0_0s")] int start,
        [Discord.Commands.Summary("remove1_1s")] int count = 1) {
        await RemoveRange(start, start + count - 1);
    }

    [RequireNonEmptyPlaylist]
    [Command("move", RunMode = RunMode.Async)]
    [Alias("m", "mv")]
    [Discord.Commands.Summary("move0s")]
    public async Task Move([Discord.Commands.Summary("move0_0s")] int trackIndex,
        [Discord.Commands.Summary("move0_1s")] int newIndex = 1) {
        // For programmers
        if (trackIndex == 0) trackIndex = 1;
        if (trackIndex < 1 || trackIndex > Player.Playlist.Count) {
            var description = new EntryLocalized("Music.TrackIndexWrong", Context.User.Mention, trackIndex,
                Player.Playlist.Count);
            await this.ReplyFailFormattedAsync(description, true).CleanupAfter(Constants.ShortTimeSpan);
        }

        newIndex = Math.Max(1, Math.Min(Player.Playlist.Count, newIndex));
        Player.Playlist.Move(trackIndex - 1, newIndex - 1);
        Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.TrackMoved", Context.User.Mention, trackIndex,
            newIndex));
    }

    [Command("playerrestart", RunMode = RunMode.Async)]
    [Discord.Commands.Summary("playerrestart0s")]
    [CommandCooldown(GuildDelayMilliseconds = 60000)]
    public async Task RestartPlayer() {
        var playerShutdownParameters = new PlayerShutdownParameters()
            { ShutdownDisplays = false, SavePlaylist = false, RestartPlayer = true };
        await Player.Shutdown(playerShutdownParameters);
    }

    [Command("changenode", RunMode = RunMode.Async)]
    [Discord.Commands.Summary("changenode0s")]
    [CommandCooldown(GuildDelayMilliseconds = 30000)]
    public async Task ChangeNode(
        [Autocomplete(typeof(LavalinkNodeAutocompleteHandler))]
        [SlashCommandOptional]
        [Discord.Commands.Summary("changenode0_0s")]
        string? node = null) {
        var currentNode = AudioService.GetPlayerNode(Player);
        var availableNodes = AudioService.Nodes
            .Where(candidate => candidate.Status == LavalinkNodeStatus.Available && candidate != currentNode)
            .ToArray();

        var targetNode = node is null
            ? availableNodes.FirstOrDefault()
            : availableNodes.FirstOrDefault(candidate =>
                string.Equals(candidate.Label, node, StringComparison.OrdinalIgnoreCase));
        if (targetNode is null) {
            var error = availableNodes.Length == 0
                ? new EntryLocalized("Music.OnlyOneNode")
                : new EntryLocalized("Music.NodeNotFound", node ?? "");
            await this.ReplyFailFormattedAsync(error);
            return;
        }

        await AudioService.MovePlayerAsync(Player, targetNode,
            new EntryLocalized("PlayerHistory.NodeChanged", Context.User.Mention));
    }
}