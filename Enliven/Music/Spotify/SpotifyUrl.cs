using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace Bot.Music.Spotify {
    public class SpotifyUrl {
        public enum SpotifyUrlType {
            Unknown,
            Album,
            Playlist,
            Track
        }

        private static readonly Regex PlaylistRegex =
            new Regex(
                @"^(https:\/\/open\.spotify\.com\/user\/spotify\/playlist\/|https:\/\/open\.spotify\.com\/playlist\/|spotify:user:spotify:playlist:|spotify:playlist:)([a-zA-Z0-9]+)(.*)$");

        private static readonly Regex TrackRegex =
            new Regex(
                @"^(https:\/\/open\.spotify\.com\/user\/spotify\/track\/|https:\/\/open\.spotify\.com\/track\/|spotify:user:spotify:track:|spotify:track:)([a-zA-Z0-9]+)(.*)$");

        private static readonly Regex AlbumRegex = new Regex(@"^(https:\/\/open\.spotify\.com\/album\/|spotify:album:)([a-zA-Z0-9]+)(.*)$");

        public SpotifyUrl(string request) {
            Request = request;
            if (TryRecognize(request, PlaylistRegex, out var id))
                Type = SpotifyUrlType.Playlist;
            else if (TryRecognize(request, TrackRegex, out id))
                Type = SpotifyUrlType.Track;
            else if (TryRecognize(request, AlbumRegex, out id))
                Type = SpotifyUrlType.Album;
            else
                Type = SpotifyUrlType.Unknown;

            Id = id;
        }

        public SpotifyUrl(string id, SpotifyUrlType type) {
            Id = id;
            Request = id;
            Type = type;
        }

        public string Id { get; private set; }
        public string Request { get; private set; }
        public SpotifyUrlType Type { get; private set; }
        public bool IsValid => Type != SpotifyUrlType.Unknown;

        private bool TryRecognize(string query, Regex regex, out string id) {
            var match = regex.Match(query);
            id = match.Success ? match.Groups[2].Value : string.Empty;
            return match.Success;
        }

        public async Task<List<SpotifyTrackWrapper>> Resolve(SpotifyClient client) {
            return (Type switch {
                SpotifyUrlType.Album => (await client.PaginateAll(await client.Albums.GetTracks(Id)))
                                       .Where(track => track != null).Select(track => new SpotifyTrackWrapper(track)),
                SpotifyUrlType.Playlist => (await client.PaginateAll((await client.Playlists.Get(Id)).Tracks!)).Select(track => track.Track as FullTrack)
                   .Where(track => track != null).Select(track => new SpotifyTrackWrapper(track!.Id, track)),
                SpotifyUrlType.Track   => new List<SpotifyTrackWrapper> {new SpotifyTrackWrapper(Id)},
                SpotifyUrlType.Unknown => throw new ArgumentOutOfRangeException(),
                _                      => throw new ArgumentOutOfRangeException()
            }).ToList();
        }
    }
}