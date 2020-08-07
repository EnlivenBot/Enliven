using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.Config;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using NLog;
using SpotifyAPI.Web;

#pragma warning disable 8604
#pragma warning disable 8602

namespace Bot.Utilities.Music {
    public class SpotifyMusicProvider : IMusicProvider {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly static Regex PlaylistRegex =
            new Regex(
                @"^(https:\/\/open\.spotify\.com\/user\/spotify\/playlist\/|https:\/\/open\.spotify\.com\/playlist\/|spotify:user:spotify:playlist:|spotify:playlist:)([a-zA-Z0-9]+)(.*)$");

        private readonly static Regex TrackRegex =
            new Regex(
                @"^(https:\/\/open\.spotify\.com\/user\/spotify\/track\/|https:\/\/open\.spotify\.com\/track\/|spotify:user:spotify:track:|spotify:track:)([a-zA-Z0-9]+)(.*)$");

        private LavalinkCluster _cluster;

        static SpotifyMusicProvider() {
            SpotifyClient = Task.Run(async () => {
                try {
                    var config = SpotifyClientConfig.CreateDefault();

                    var request = new ClientCredentialsRequest(GlobalConfig.Instance.SpotifyClientID, GlobalConfig.Instance.SpotifyClientSecret);
                    // If credentials wrong, this \/ line will throw the exception
                    await new OAuthClient(config).RequestToken(request);

                    var actualConfig = SpotifyClientConfig
                                      .CreateDefault()
                                      .WithAuthenticator(new ClientCredentialsAuthenticator(GlobalConfig.Instance.SpotifyClientID,
                                           GlobalConfig.Instance.SpotifyClientSecret));
                    return new SpotifyClient(actualConfig);
                }
                catch (Exception e) {
                    logger.Error(e, "Wrong Spotify credentials. Check config file");
                    return null;
                }
            });
        }

        public SpotifyMusicProvider(LavalinkCluster cluster, string query) {
            _cluster = cluster;
            var playlistMatch = PlaylistRegex.Match(query);
            var trackMatch = TrackRegex.Match(query);
            IsPlaylist = playlistMatch.Success ? true : trackMatch.Success ? (bool?) false : null;
            if (IsPlaylist != null) {
                Id = ((bool) IsPlaylist ? playlistMatch : trackMatch).Groups[2].Value;
            }
        }

        public static Task<SpotifyClient?> SpotifyClient { get; private set; }

        // True - playlist
        // False - Track
        // Null - Fetch error
        private bool? IsPlaylist { get; set; }

        private string? Id { get; set; }

        public async Task<bool> CanProvide() {
            if (await SpotifyClient == null) {
                return false;
            }

            return IsPlaylist != null;
        }

        public async Task<List<LavalinkTrack>> Provide() {
            var spotify = await SpotifyClient;
            List<string> tracksIds = new List<string>();
            switch (IsPlaylist) {
                case true: {
                    var playlist = await spotify.Playlists.Get(Id);
                    tracksIds = (await spotify.PaginateAll(playlist.Tracks)).Select(track => (track.Track as FullTrack)?.Id).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()!;
                    break;
                }
                case false: {
                    tracksIds.Add(Id);
                    break;
                }
            }

            var enumerable = tracksIds.Select(s => ResolveWithCache(s, _cluster));
            return (await Task.WhenAll(enumerable)).ToList();
        }

        public static void Initialize() {
            // Dummy method to initialize static properties
        }

        public static async Task<LavalinkTrack> ResolveWithCache(string spotifyTrackId, LavalinkCluster lavalinkCluster) {
            if (TryGetFromCache(spotifyTrackId, out var track)) {
                return track!;
            }

            var fullTrack = await (await SpotifyClient).Tracks.Get(spotifyTrackId);
            var lavalinkTracks = await new DefaultMusicProvider(lavalinkCluster, $"{fullTrack.Name} - {fullTrack.Artists[0].Name}").Provide();
            try {
                GlobalDB.SpotifyAssociations.Upsert(new SpotifyTrackAssociation(spotifyTrackId, lavalinkTracks[0].Identifier));
                return lavalinkTracks[0];
            }
            catch (Exception) {
                // ignored
            }

            return null!;
        }

        public static bool TryGetFromCache(string spotifyTrackId, out LavalinkTrack? track) {
            track = default!;
            try {
                track = GlobalDB.SpotifyAssociations.FindById(spotifyTrackId)?.GetBestAssociation().Association;
                return track != null;
            }
            catch (Exception) {
                return false;
            }
        }
    }
}