using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Enliven.MusicResolvers.Base;
using SpotifyAPI.Web;

namespace Enliven.MusicResolver.Spotify;

public class SpotifyUrl {
    private const string PlaylistPattern =
        @"^(https:\/\/open\.spotify\.com\/user\/spotify\/playlist\/|https:\/\/open\.spotify\.com\/playlist\/|spotify:user:spotify:playlist:|spotify:playlist:)([a-zA-Z0-9]+)(.*)$";

    private const string TrackPattern =
        @"^(https:\/\/open\.spotify\.com\/user\/spotify\/track\/|https:\/\/open\.spotify\.com\/track\/|spotify:user:spotify:track:|spotify:track:)([a-zA-Z0-9]+)(.*)$";

    private const string AlbumPattern = @"^(https:\/\/open\.spotify\.com\/album\/|spotify:album:)([a-zA-Z0-9]+)(.*)$";

    private const string ArtistPattern =
        @"^(https:\/\/open\.spotify\.com\/artist\/|spotify:artist:)([a-zA-Z0-9]+)(.*)$";

    private static readonly Regex PlaylistRegex = new(PlaylistPattern);
    private static readonly Regex TrackRegex = new(TrackPattern);
    private static readonly Regex AlbumRegex = new(AlbumPattern);
    private static readonly Regex ArtistRegex = new(ArtistPattern);

    public SpotifyUrl(string request) {
        Request = request;
        if (!request.StartsWith("https://open.spotify.com")) {
            Type = AudioUrlType.Unknown;
            Id = request;
            return;
        }

        if (TryRecognize(request, PlaylistRegex, out var id))
            Type = AudioUrlType.Playlist;
        else if (TryRecognize(request, TrackRegex, out id))
            Type = AudioUrlType.Track;
        else if (TryRecognize(request, AlbumRegex, out id))
            Type = AudioUrlType.Album;
        else if (TryRecognize(request, ArtistRegex, out id))
            Type = AudioUrlType.Artist;
        else
            Type = AudioUrlType.Unknown;

        Id = id;
    }

    public SpotifyUrl(string id, AudioUrlType type) {
        Id = id;
        Request = id;
        Type = type;
    }

    public string Id { get; private set; }
    public string Request { get; private set; }
    public AudioUrlType Type { get; private set; }
    public bool IsValid => Type != AudioUrlType.Unknown;

    private static bool TryRecognize(string query, Regex regex, out string id) {
        var match = regex.Match(query);
        id = match.Success ? match.Groups[2].Value : string.Empty;
        return match.Success;
    }

    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
    public async Task<IReadOnlyCollection<SpotifyTrackWrapper>> Resolve(SpotifyClient client) {
        return (Type switch {
            AudioUrlType.Album => await ResolveAlbum(client, Id),
            AudioUrlType.Playlist => await ResolvePlaylist(client, Id),
            AudioUrlType.Track => new List<SpotifyTrackWrapper> { new(Id) },
            AudioUrlType.Artist => await ResolveArtist(client, Id),
            AudioUrlType.Unknown => throw new NotSupportedException(),
            AudioUrlType.User => throw new NotSupportedException(),
            _ => throw new ArgumentOutOfRangeException()
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