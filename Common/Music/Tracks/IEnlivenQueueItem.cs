using System;
using Lavalink4NET.Players;
using Lavalink4NET.Tracks;

namespace Common.Music.Tracks;

public interface IEnlivenQueueItem : ITrackQueueItem
{
    TrackRequester Requester { get; }

    new LavalinkTrack Track => Reference.Track!;
    TrackPlaylist? Playlist { get; }

    new T? As<T>() where T : class, IEnlivenQueueItem => this as T;
    
    uint PlaybackExceptionCount { get; set; }

    IEnlivenQueueItem WithStartPosition(TimeSpan position);
}