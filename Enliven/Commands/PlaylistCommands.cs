using System;
using System.Threading.Tasks;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Common;
using Common.Music;
using Discord.Commands;
using LiteDB;

namespace Bot.Commands {
    [SlashCommandAdapter]
    public sealed class PlaylistCommands : MusicModuleBase {
        public IPlaylistProvider PlaylistProvider { get; set; } = null!;

        [Hidden]
        [RequireNonEmptyPlaylist]
        [Command("saveplaylist", RunMode = RunMode.Async)]
        [Alias("sp")]
        [Summary("saveplaylist0s")]
        public async Task SavePlaylist() {
            var playlist = await Player.ExportPlaylist(ExportPlaylistOptions.IgnoreTrackIndex);
            var storedPlaylist = PlaylistProvider.StorePlaylist(playlist, "u" + ObjectId.NewObjectId(), Context.User.ToLink());
            await ReplyFormattedAsync(Loc.Get("Music.PlaylistSaved", storedPlaylist.Id, GuildConfig.Prefix));
        }

        [ShouldCreatePlayer]
        [Command("loadplaylist", RunMode = RunMode.Async)]
        [Alias("lp")]
        [Summary("loadplaylist0s")]
        public async Task LoadPlaylist([Summary("playlistId")] [Remainder] string id) {
            await ExecutePlaylist(id, ImportPlaylistOptions.Replace);
        }

        [ShouldCreatePlayer]
        [Command("addplaylist", RunMode = RunMode.Async)]
        [Alias("ap")]
        [Summary("addplaylist0s")]
        public async Task AddPlaylist([Summary("playlistId")] [Remainder] string id) {
            await ExecutePlaylist(id, ImportPlaylistOptions.JustAdd);
        }

        [ShouldCreatePlayer]
        [Command("runplaylist", RunMode = RunMode.Async)]
        [Alias("rp")]
        [Summary("runplaylist0s")]
        public async Task RunPlaylist([Summary("playlistId")] [Remainder] string id) {
            await ExecutePlaylist(id, ImportPlaylistOptions.AddAndPlay);
        }

        private async Task ExecutePlaylist(string id, ImportPlaylistOptions options) {
            var playlist = PlaylistProvider.Get(id);
            if (playlist == null) {
                await ReplyFormattedAsync(Loc.Get("Music.PlaylistNotFound", id.SafeSubstring(100, "...") ?? ""), true);
                return;
            }

            Player!.WriteToQueueHistory(Loc.Get("Music.LoadPlaylist", Context.User.Username,
                id.SafeSubstring(100, "...") ?? ""));
            await Player.ImportPlaylist(playlist, options, Context.User.Username);
            var mainPlayerDisplay = await GetMainPlayerDisplay();
            _ = mainPlayerDisplay.ControlMessageResend();
            Context?.Message?.SafeDelete();
        }
    }
}