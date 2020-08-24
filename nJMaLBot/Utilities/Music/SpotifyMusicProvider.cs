using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Music;
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
            IsPlaylist = !IsTrack(query, out var id);
            Id = id;
        }

        /// <returns>True - track, False - playlist, null - error</returns>
        public static bool? IsTrack(string query, out string? id) {
            id = null;
            var playlistMatch = PlaylistRegex.Match(query);
            var trackMatch = TrackRegex.Match(query);
            var isTrack = trackMatch.Success ? true : playlistMatch.Success ? (bool?) false : null;
            if (isTrack != null) {
                id = ((bool) isTrack ? trackMatch : playlistMatch).Groups[2].Value;
            }

            return isTrack;
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
            try {
                var spotify = await SpotifyClient;
                List<SpotifyTrackData> tracksData = new List<SpotifyTrackData>();
                switch (IsPlaylist) {
                    case true: {
                        var playlist = await spotify.Playlists.Get(Id);
                        tracksData = (await spotify.PaginateAll(playlist.Tracks))
                                    .Select(track => (track.Track as FullTrack))
                                    .Where(track => track != null)
                                    .Select(track => new SpotifyTrackData(track.Id, track)).ToList()!;
                        break;
                    }
                    case false: {
                        tracksData.Add(new SpotifyTrackData(Id));
                        break;
                    }
                }

                var enumerable = tracksData.Select(s => ResolveWithCache(s, _cluster));
                return (await Task.WhenAll(enumerable)).Select(association => association?.GetBestAssociation()?.Association).ToList()!;
            }
            catch (Exception) {
                throw new NothingFoundException(false);
            }
        }

        public static void Initialize() {
            // Dummy method to initialize static properties
        }

        public static async Task<SpotifyTrackAssociation?> ResolveWithCache(SpotifyTrackData spotifyTrack, LavalinkCluster lavalinkCluster) {
            if (TryGetFromCache(spotifyTrack.Id, out var track)) {
                return track!;
            }

            try {
                var fullTrack = await spotifyTrack.GetTrack();
                var lavalinkTracks = await new DefaultMusicProvider(lavalinkCluster, $"{fullTrack.Name} - {fullTrack.Artists[0].Name}").Provide();
                var spotifyTrackAssociation = new SpotifyTrackAssociation(spotifyTrack.Id, lavalinkTracks[0].Identifier);
                spotifyTrackAssociation.Save();
                return spotifyTrackAssociation;
            }
            catch (Exception) {
                // ignored
            }

            return null;
        }

        public static bool TryGetFromCache(string spotifyTrackId, out SpotifyTrackAssociation? track) {
            track = default!;
            try {
                track = GlobalDB.SpotifyAssociations.FindById(spotifyTrackId);
                return track != null;
            }
            catch (Exception) {
                return false;
            }
        }
    }
}