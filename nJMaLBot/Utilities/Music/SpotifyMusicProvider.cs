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

        private readonly static Regex _playlistRegex =
            new Regex(
                @"^(https:\/\/open\.spotify\.com\/user\/spotify\/playlist\/|https:\/\/open\.spotify\.com\/playlist\/|spotify:user:spotify:playlist:|spotify:playlist:)([a-zA-Z0-9]+)(.*)$");

        private readonly static Regex _trackRegex =
            new Regex(
                @"^(https:\/\/open\.spotify\.com\/user\/spotify\/track\/|https:\/\/open\.spotify\.com\/track\/|spotify:user:spotify:track:|spotify:track:)([a-zA-Z0-9]+)(.*)$");

        private LavalinkCluster _cluster;
        private string _query;

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
            _query = query;
            _cluster = cluster;
            var playlistMatch = _playlistRegex.Match(query);
            var trackMatch = _trackRegex.Match(query);
            IsPlaylist = playlistMatch.Success ? true : trackMatch.Success ? (bool?) false : null;
            if (IsPlaylist != null) {
                Id = ((bool) IsPlaylist ? playlistMatch : trackMatch).Groups[2].Value;
            }
        }

        public static Task<SpotifyClient?> SpotifyClient { get; private set; }

        // True - playlist
        // False - Track
        // Null - Fetch error
        private bool? IsPlaylist { get; set; } = null;

        private string? Id { get; set; }

        public async Task<bool> CanProvide() {
            if (await SpotifyClient == null) {
                return false;
            }

            return IsPlaylist != null;
        }

        public async Task<List<LavalinkTrack>> Provide() {
            var spotify = await SpotifyClient;
            List<IPlayableItem> tracks = new List<IPlayableItem>();
            switch (IsPlaylist) {
                case true: {
                    var playlist = await spotify.Playlists.Get(Id);
                    tracks = (await spotify.PaginateAll(playlist.Tracks)).Select(track => track.Track).ToList();
                    break;
                }
                case false: {
                    var track = await spotify.Tracks.Get(Id);
                    tracks.Add(track);
                    break;
                }
                default:
                    throw new NotSupportedException();
            }
            var enumerable = tracks.Where(track => track is FullTrack).Select(async track => {
                var fullTrack = track as FullTrack;
                return await new DefaultMusicProvider(_cluster, $"{fullTrack.Name} - {fullTrack.Artists[0].Name}").Provide();
            });
            var whenAll = await Task.WhenAll(enumerable);
            return whenAll.SelectMany(list => list).ToList();
        }

        public static void Initialize() {
            // Dummy method to initialize static properties
        }
    }
}