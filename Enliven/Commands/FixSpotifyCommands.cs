using System;
using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.DiscordRelated.Commands.Modules;
using Bot.Music.Spotify;
using Common;
using Common.Config;
using Discord.Commands;

namespace Bot.Commands {
    public class FixSpotifyCommands : MusicModuleBase {
        public IUserDataProvider UserDataProvider { get; set; } = null!;
        public ISpotifyAssociationProvider SpotifyAssociationProvider { get; set; } = null!;
        public ISpotifyAssociationCreator SpotifyAssociationCreator { get; set; } = null!;
        public SpotifyMusicResolver Resolver { get; set; } = null!;

        [Command("fixspotify", RunMode = RunMode.Async)]
        [Alias("spotify, fs")]
        [Summary("fixspotify0s")]
        public async Task FixSpotify() {
            if (!await IsPreconditionsValid) return;
            if (Player == null) {
                await ErrorMessageController.AddEntry(String.Format(GuildConfig.Prefix))
                                            .UpdateTimeout(Constants.StandardTimeSpan).Update();
                return;
            }

            if (Player.CurrentTrack is SpotifyLavalinkTrack spotifyLavalinkTrack) {
                var fixSpotifyChain = FixSpotifyChain.CreateInstance(Context.User, Context.Channel, Loc,
                    $"spotify:track:{spotifyLavalinkTrack.RelatedSpotifyTrackWrapper.Id}", MusicController, UserDataProvider,
                    SpotifyAssociationProvider, SpotifyAssociationCreator, Resolver);
                await fixSpotifyChain.Start();
            }
            else {
                await ErrorMessageController.AddEntry(Loc.Get("Music.CurrentTrackNonSpotify"))
                                            .UpdateTimeout(Constants.StandardTimeSpan).Update();
            }
        }

        [Command("fixspotify", RunMode = RunMode.Async)]
        [Alias("spotify, fs")]
        [Summary("fixspotify0s")]
        public async Task FixSpotify([Remainder] [Summary("fixspotify0_0s")]
                                     string s) {
            var fixSpotifyChain = FixSpotifyChain.CreateInstance(Context.User, Context.Channel, Loc, s, MusicController, 
                UserDataProvider, SpotifyAssociationProvider, SpotifyAssociationCreator, Resolver);
            await fixSpotifyChain.Start();
        }
    }
}