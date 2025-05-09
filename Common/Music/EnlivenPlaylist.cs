﻿using System;
using Common.Config;
using LiteDB;

namespace Common.Music;

public interface IPlaylistProvider
{
    StoredPlaylist StorePlaylist(EnlivenPlaylist enlivenPlaylist, UserLink authorId);
    StoredPlaylist StorePlaylist(EnlivenPlaylist enlivenPlaylist, string id, UserLink authorId);
    StoredPlaylist? Get(string id);
}

public class PlaylistProvider(ILiteCollection<StoredPlaylist> liteCollection) : IPlaylistProvider
{
    public StoredPlaylist StorePlaylist(EnlivenPlaylist enlivenPlaylist, UserLink authorId)
    {
        return StorePlaylist(enlivenPlaylist, ObjectId.NewObjectId().ToString(), authorId);
    }

    public StoredPlaylist StorePlaylist(EnlivenPlaylist enlivenPlaylist, string id, UserLink authorId)
    {
        var storedPlaylist = new StoredPlaylist
        {
            Playlist = enlivenPlaylist,
            Id = id, Author = authorId, CreationTime = DateTime.UtcNow
        };

        liteCollection.Upsert(storedPlaylist);

        return storedPlaylist;
    }

    public StoredPlaylist? Get(string id)
    {
        return liteCollection.FindById(id);
    }
}

public class EnlivenPlaylist
{
    public byte[][] Tracks { get; set; } = [];
    public int TrackIndex { get; set; } = -1;
    public TimeSpan? TrackPosition { get; set; }
}

public class StoredPlaylist
{
    [BsonId] public required string Id { get; set; }
    public required DateTime CreationTime { get; set; }
    public required UserLink Author { get; set; }

    public required EnlivenPlaylist Playlist { get; set; }
}

public enum ExportPlaylistOptions
{
    AllData,
    IgnoreTrackPosition,
    IgnoreTrackIndex
}

public enum ImportPlaylistOptions
{
    Replace,
    AddAndPlay,
    JustAdd
}