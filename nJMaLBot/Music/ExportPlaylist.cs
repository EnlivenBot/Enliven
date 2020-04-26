using System;
using System.Collections.Generic;
using Bot.Config;
using LiteDB;

namespace Bot.Music {
    public class ExportPlaylist {
        public List<string> Tracks { get; set; } = new List<string>();
        public int TrackIndex { get; set; } = -1;
        public TimeSpan? TrackPosition { get; set; }

        public StoredPlaylist StorePlaylist(object id) {
            var storedPlaylist = new StoredPlaylist {
                Tracks = Tracks, TrackIndex = TrackIndex, TrackPosition = TrackPosition,
                Id = id
            };
            
            GlobalDB.Playlists.Upsert(storedPlaylist);

            return storedPlaylist;
        }
    }

    public class StoredPlaylist : ExportPlaylist {
        [BsonId] public object Id { get; set; }
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