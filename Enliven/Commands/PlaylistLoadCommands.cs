using System.Threading.Tasks;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Common;
using Common.Localization.Entries;
using Common.Music;
using Common.Music.Tracks;
using Discord.Commands;

namespace Bot.Commands;

[SlashCommandAdapter]
public sealed class PlaylistLoadCommands : CreatePlayerMusicModuleBase
{
    public IPlaylistProvider PlaylistProvider { get; set; } = null!;

    [Command("loadplaylist", RunMode = RunMode.Async)]
    [Alias("lp")]
    [Summary("loadplaylist0s")]
    public async Task LoadPlaylist([Summary("playlistId")] [Remainder] string id)
    {
        await ExecutePlaylist(id, ImportPlaylistOptions.Replace);
    }

    [Command("addplaylist", RunMode = RunMode.Async)]
    [Alias("ap")]
    [Summary("addplaylist0s")]
    public async Task AddPlaylist([Summary("playlistId")] [Remainder] string id)
    {
        await ExecutePlaylist(id, ImportPlaylistOptions.JustAdd);
    }

    [Command("runplaylist", RunMode = RunMode.Async)]
    [Alias("rp")]
    [Summary("runplaylist0s")]
    public async Task RunPlaylist([Summary("playlistId")] [Remainder] string id)
    {
        await ExecutePlaylist(id, ImportPlaylistOptions.AddAndPlay);
    }

    private async Task ExecutePlaylist(string id, ImportPlaylistOptions options)
    {
        var playlist = PlaylistProvider.Get(id);
        if (playlist == null)
        {
            await this.ReplyFailFormattedAsync(
                new EntryLocalized("Music.PlaylistNotFound", id.SafeSubstring(100, "...") ?? ""), true);
            return;
        }

        Player.WriteToQueueHistory(Loc.Get("Music.LoadPlaylist", Context.User.Username,
            id.SafeSubstring(100, "...")));
        await Player.ImportPlaylist(playlist.Playlist, options, new TrackRequester(Context.User));
        // TODO: Make control message resending as command response
        var mainPlayerDisplay = await GetMainPlayerDisplay();
        _ = mainPlayerDisplay.ControlMessageResend();
        await this.RemoveMessageInvokerIfPossible();
    }
}