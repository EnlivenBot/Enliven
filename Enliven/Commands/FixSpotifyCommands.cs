using System;
using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.DiscordRelated;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.MessageComponents;
using Bot.Music.Spotify;
using Bot.Utilities.Collector;
using Common;
using Common.Config;
using Discord.Commands;

namespace Bot.Commands {
    [SlashCommandAdapter]
    public class FixSpotifyCommands : MusicModuleBase {
        public IUserDataProvider UserDataProvider { get; set; } = null!;
        public ISpotifyAssociationProvider SpotifyAssociationProvider { get; set; } = null!;
        public ISpotifyAssociationCreator SpotifyAssociationCreator { get; set; } = null!;
        public SpotifyMusicResolver Resolver { get; set; } = null!;
        public SpotifyClientResolver SpotifyClientResolver { get; set; } = null!;
        public MessageComponentService MessageComponentService { get; set; } = null!;
        public CollectorService CollectorService { get; set; } = null!;

        [Command("fixspotify", RunMode = RunMode.Async)]
        [Alias("spotify, fs")]
        [Summary("fixspotify0s")]
        public async Task FixSpotify([Remainder] [Summary("fixspotify0_0s")] string? s = null) {
            if (s == null) {
                if (Player.CurrentTrack is SpotifyLavalinkTrack spotifyLavalinkTrack) {
                    var request = $"spotify:track:{spotifyLavalinkTrack.RelatedSpotifyTrackWrapper.Id}";
                    var fixSpotifyChain = new FixSpotifyChain(Context.User, Context.Channel, Loc,
                        request, MusicController, UserDataProvider, SpotifyAssociationCreator, SpotifyClientResolver,
                        MessageComponentService, CollectorService, Context.Client);
                    await fixSpotifyChain.Start();
                }
                else {
                    await ReplyFormattedAsync(Loc.Get("Music.CurrentTrackNonSpotify"), true);
                }
            }
            else {
                var fixSpotifyChain = new FixSpotifyChain(Context.User, Context.Channel, Loc, s,
                    MusicController, UserDataProvider, SpotifyAssociationCreator, SpotifyClientResolver,
                    MessageComponentService, CollectorService, Context.Client);
                await fixSpotifyChain.Start();
            }
        }
    }
}