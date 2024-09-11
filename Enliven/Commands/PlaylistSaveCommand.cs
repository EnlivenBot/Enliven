using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Common;
using Common.Localization.Entries;
using Common.Music;
using Discord.Commands;
using LiteDB;

namespace Bot.Commands;

[SlashCommandAdapter]
public sealed class PlaylistSaveCommand : HavePlayerMusicModuleBase
{
    public IPlaylistProvider PlaylistProvider { get; set; } = null!;

    [Hidden]
    [RequireNonEmptyPlaylist]
    [Command("saveplaylist", RunMode = RunMode.Async)]
    [Alias("sp")]
    [Summary("saveplaylist0s")]
    public async Task SavePlaylist()
    {
        var playlist = await Player.ExportPlaylist(ExportPlaylistOptions.IgnoreTrackIndex);
        var storedPlaylist =
            PlaylistProvider.StorePlaylist(playlist, "u" + ObjectId.NewObjectId(), Context.User.ToLink());
        await this.ReplySuccessFormattedAsync(new EntryLocalized("Music.PlaylistSaved", storedPlaylist.Id));
    }
}