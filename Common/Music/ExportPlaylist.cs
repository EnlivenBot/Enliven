using System;
using System.Collections.Generic;
using Common.Config;
using LiteDB;

namespace Common.Music {
    public class ExportPlaylist {
        public List<string> Tracks { get; set; } = new List<string>();
        public int TrackIndex { get; set; } = -1;
        public TimeSpan? TrackPosition { get; set; }

        public StoredPlaylist StorePlaylist(object id, ulong authorId) {
            var storedPlaylist = new StoredPlaylist {
                Tracks = Tracks, TrackIndex = TrackIndex, TrackPosition = TrackPosition,
                Id = id, AuthorId = authorId, CreationTime = DateTime.Now
            };
            
            Database.Playlists.Upsert(storedPlaylist);

            return storedPlaylist;
        }
    }

    public class StoredPlaylist : ExportPlaylist {
        [BsonId] public object Id { get; set; } = null!;
        public DateTime CreationTime { get; set; }
        public ulong AuthorId { get; set; }
    }

    public enum ExportPlaylistOptions {
        AllData,
        IgnoreTrackPosition,
        IgnoreTrackIndex
    }

    public enum ImportPlaylistOptions {
        Replace,
        AddAndPlay,
        JustAdd
    }
}