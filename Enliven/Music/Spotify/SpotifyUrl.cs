using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common;
using SpotifyAPI.Web;

namespace Bot.Music.Spotify {
    public class SpotifyUrl {
        private const string PlaylistPattern = @"^(https:\/\/open\.spotify\.com\/user\/spotify\/playlist\/|https:\/\/open\.spotify\.com\/playlist\/|spotify:user:spotify:playlist:|spotify:playlist:)([a-zA-Z0-9]+)(.*)$";
        private const string TrackPattern = @"^(https:\/\/open\.spotify\.com\/user\/spotify\/track\/|https:\/\/open\.spotify\.com\/track\/|spotify:user:spotify:track:|spotify:track:)([a-zA-Z0-9]+)(.*)$";
        private const string AlbumPattern = @"^(https:\/\/open\.spotify\.com\/album\/|spotify:album:)([a-zA-Z0-9]+)(.*)$";
        private const string ArtistPattern = @"^(https:\/\/open\.spotify\.com\/artist\/|spotify:artist:)([a-zA-Z0-9]+)(.*)$";
        public enum SpotifyUrlType {
            Unknown,
            Album,
            Playlist,
            Track,
            Artist
        }

        private static readonly Regex PlaylistRegex = new Regex(PlaylistPattern);
        private static readonly Regex TrackRegex = new Regex(TrackPattern);
        private static readonly Regex AlbumRegex = new Regex(AlbumPattern);
        private static readonly Regex ArtistRegex = new Regex(ArtistPattern);

        public SpotifyUrl(string request) {
            Request = request;
            if (TryRecognize(request, PlaylistRegex, out var id))
                Type = SpotifyUrlType.Playlist;
            else if (TryRecognize(request, TrackRegex, out id))
                Type = SpotifyUrlType.Track;
            else if (TryRecognize(request, AlbumRegex, out id))
                Type = SpotifyUrlType.Album;
            else if (TryRecognize(request, ArtistRegex, out id))
                Type = SpotifyUrlType.Artist;
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

        private static bool TryRecognize(string query, Regex regex, out string id) {
            var match = regex.Match(query);
            id = match.Success ? match.Groups[2].Value : string.Empty;
            return match.Success;
        }

        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        public async Task<List<SpotifyTrackWrapper>> Resolve(SpotifyClient client) {
            return (Type switch {
                SpotifyUrlType.Album    => await ResolveAlbum(client, Id),
                SpotifyUrlType.Playlist => await ResolvePlaylist(client, Id),
                SpotifyUrlType.Track    => new List<SpotifyTrackWrapper> { new SpotifyTrackWrapper(Id) },
                SpotifyUrlType.Artist   => await ResolveArtist(client, Id),
                SpotifyUrlType.Unknown  => throw new ArgumentOutOfRangeException(),
                _                       => throw new ArgumentOutOfRangeException()
            }).ToList();
        }

        private static async Task<IEnumerable<SpotifyTrackWrapper>> ResolveAlbum(SpotifyClient client, string id) {
            var tracksPaginable = await client.Albums.GetTracks(id);
            var tracks = await client.PaginateAll(tracksPaginable);
            return tracks.Where(track => track != null).Select(track => new SpotifyTrackWrapper(track));
        }

        private static async Task<IEnumerable<SpotifyTrackWrapper>> ResolvePlaylist(SpotifyClient client, string id) {
            var fullPlaylist = await client.Playlists.Get(id);
            var tracks = await client.PaginateAll(fullPlaylist.Tracks!);
            return tracks.Select(track => track.Track as FullTrack)
                .Where(track => track != null)
                .Select(track => new SpotifyTrackWrapper(track!.Id, track));
        }

        private static async Task<IEnumerable<SpotifyTrackWrapper>> ResolveArtist(SpotifyClient client, string id) {
            var topTracks = await client.Artists.GetTopTracks(id, new ArtistsTopTracksRequest("ES"));
            return topTracks.Tracks.Select(track => new SpotifyTrackWrapper(track.Id, track));
        }
    }
}