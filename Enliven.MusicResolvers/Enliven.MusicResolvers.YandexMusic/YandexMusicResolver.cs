using System.Collections.Immutable;
using System.Text.Json;
using Common;
using Enliven.MusicResolvers.Base;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using YandexMusicResolver;

namespace Enliven.MusicResolver.YandexMusic;

public class YandexMusicResolver(IYandexMusicMainResolver musicMainResolver)
    : MusicResolverBase<YandexLavalinkTrack, YandexTrackData> {
    public override bool IsAvailable => true;
    public override bool CanResolve(string query) => musicMainResolver.CanResolveQuery(query, false);

    public override async ValueTask<MusicResolveResult> Resolve(ITrackManager cluster,
        LavalinkApiResolutionScope resolutionScope,
        string query, CancellationToken cancellationToken) {
        var yandexMusicSearchResult = await musicMainResolver.ResolveQuery(query, true, false);
        if (yandexMusicSearchResult == null) return TrackLoadResult.CreateEmpty();
        var tracks = new List<LavalinkTrack>();
        PlaylistInformation? playlistInfo = null;

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (yandexMusicSearchResult.Type) {
            case YandexSearchType.Track:
                var yandexMusicTrack = yandexMusicSearchResult.Tracks?.FirstOrDefault();
                if (yandexMusicTrack is not null && yandexMusicTrack.IsAvailable) {
                    tracks.Add(new YandexLavalinkTrack(yandexMusicTrack, musicMainResolver.DirectUrlLoader));
                }

                break;
            case YandexSearchType.Album:
                var yandexMusicAlbum = yandexMusicSearchResult.Albums?.FirstOrDefault();
                if (yandexMusicAlbum is not null) {
                    var yandexLavalinkTracks = await yandexMusicAlbum.LoadDataAsync()
                        .PipeAsync(x => x
                            .Where(track => track.IsAvailable)
                            .Select(track => new YandexLavalinkTrack(track, musicMainResolver.DirectUrlLoader)));
                    tracks.AddRange(yandexLavalinkTracks);


                    playlistInfo = new PlaylistInformation(yandexMusicAlbum.Title, null,
                        ImmutableDictionary<string, JsonElement>.Empty);
                }

                break;
            case YandexSearchType.Playlist:
                var yandexMusicPlaylist = yandexMusicSearchResult.Playlists?.FirstOrDefault();
                if (yandexMusicPlaylist is not null) {
                    var yandexLavalinkTracks = await yandexMusicPlaylist.LoadDataAsync()
                        .PipeAsync(x => x
                            .Where(track => track.IsAvailable)
                            .Select(track => new YandexLavalinkTrack(track, musicMainResolver.DirectUrlLoader)));
                    tracks.AddRange(yandexLavalinkTracks);

                    playlistInfo = new PlaylistInformation(yandexMusicPlaylist.Title, null,
                        ImmutableDictionary<string, JsonElement>.Empty);
                }

                break;
        }

        return new TrackLoadResult(tracks.ToArray(), playlistInfo!);
    }

    protected override ValueTask<IEncodedTrack> EncodeTrackInternal(YandexLavalinkTrack track) {
        return new ValueTask<IEncodedTrack>(new YandexTrackData(track.RelatedYandexTrack.Id));
    }

    public override async ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracksInternal(
        IEnumerable<YandexTrackData> tracks) {
        var yandexIds = tracks.Select(data => data.Id);
        return await yandexIds.Chunk(400)
            .Select(async chunk => await musicMainResolver.TrackLoader.LoadTracks(chunk))
            .WhenAll()
            .PipeAsync(collections => collections.SelectMany(c => c))
            .PipeAsync(collection => collection
                .Select(track => new YandexLavalinkTrack(track, musicMainResolver.DirectUrlLoader))
                .ToImmutableArray());
    }
}