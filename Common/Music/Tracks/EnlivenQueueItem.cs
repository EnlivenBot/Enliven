using System;
using Lavalink4NET.Players;
using Lavalink4NET.Tracks;

namespace Common.Music.Tracks;

public class EnlivenQueueItem : IEnlivenQueueItem
{
    public EnlivenQueueItem(LavalinkTrack track, TrackRequester requester, TrackPlaylist? playlist = null)
    {
        Reference = new TrackReference(track);
        Requester = requester;
        Playlist = playlist;
    }

    public TrackReference Reference { get; }
    public TrackPlaylist? Playlist { get; }
    public LavalinkTrack Track => Reference.Track!;
    public TrackRequester Requester { get; }

    public uint PlaybackExceptionCount { get; set; }

    public IEnlivenQueueItem WithStartPosition(TimeSpan position)
    {
        var newTrack = Track with { StartPosition = position };
        return new EnlivenQueueItem(newTrack, Requester, Playlist);
    }
}