using System;
using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Common;
using Common.Music;
using Common.Music.Controller;
using Discord.Commands;
using LiteDB;

namespace Bot.Commands {
    public sealed class PlaylistCommands : MusicModuleBase {
        public IPlaylistProvider PlaylistProvider { get; set; } = null!;

        [Hidden]
        [Command("saveplaylist", RunMode = RunMode.Async)]
        [Alias("sp")]
        [Summary("saveplaylist0s")]
        public async Task SavePlaylist() {
            if (!await IsPreconditionsValid) return;
            if (Player == null || Player.Playlist.IsEmpty) {
                await ErrorMessageController.AddEntry(String.Format(GuildConfig.Prefix)).UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            var playlist = Player.ExportPlaylist(ExportPlaylistOptions.IgnoreTrackIndex);
            var storedPlaylist = PlaylistProvider.StorePlaylist(playlist, "u" + ObjectId.NewObjectId(), Context.User.ToLink());
            await ReplyFormattedAsync(Loc.Get("Music.PlaylistSaved", storedPlaylist.Id, GuildConfig.Prefix));
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

            var playlist = PlaylistProvider.Get(id);
            if (playlist == null) {
                await ErrorMessageController.AddEntry(Loc.Get("Music.PlaylistNotFound", id.SafeSubstring(100, "...") ?? ""))
                                            .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            Player!.WriteToQueueHistory(Loc.Get("Music.LoadPlaylist", Context.User.Username,
                id.SafeSubstring(100, "...") ?? ""));
            await Player.ImportPlaylist(playlist, options, Context.User.Username);
            Context?.Message?.SafeDelete();
        }
    }
}