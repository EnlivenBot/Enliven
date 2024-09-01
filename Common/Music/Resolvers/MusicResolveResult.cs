using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Tyrrrz.Extensions;

namespace Common.Music.Resolvers;

public class MusicResolveResult
{
    private static readonly IAsyncEnumerable<LavalinkTrack> EmptyLavalinkTracks =
        Array.Empty<LavalinkTrack>().ToAsyncEnumerable();

    public MusicResolveResult(IReadOnlyList<LavalinkTrack> tracks, PlaylistInformation? playlistInformation = null) :
        this(tracks)
    {
        Playlist = playlistInformation;
    }

    public MusicResolveResult(IReadOnlyList<LavalinkTrack> tracks)
    {
        Tracks = tracks.ToAsyncEnumerable();
    }

    public MusicResolveResult(IAsyncEnumerable<LavalinkTrack> tracks, string? playlistName = null) :
        this(tracks)
    {
        if (playlistName != null)
            Playlist = new PlaylistInformation(playlistName, null, ImmutableDictionary<string, JsonElement>.Empty);
    }

    public MusicResolveResult(IAsyncEnumerable<LavalinkTrack> tracks)
    {
        Tracks = tracks;
    }

    public MusicResolveResult(TrackException? exception)
    {
        Tracks = EmptyLavalinkTracks;
        Exception = exception;
    }

    public IAsyncEnumerable<LavalinkTrack> Tracks { get; init; }
    public PlaylistInformation? Playlist { get; init; }
    public TrackException? Exception { get; init; }

    public static implicit operator MusicResolveResult(TrackLoadResult trackLoadResult)
    {
        return trackLoadResult.IsFailed
            ? new MusicResolveResult(trackLoadResult.Exception)
            : new MusicResolveResult(trackLoadResult.Tracks, trackLoadResult.Playlist);
    }
}