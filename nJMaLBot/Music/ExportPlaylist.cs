using System;
using System.Collections.Generic;
using LiteDB;

namespace Bot.Music {
    public class ExportPlaylist {
        public List<string> Tracks { get; set; } = new List<string>();
        public int TrackIndex { get; set; } = -1;
        public TimeSpan? TrackPosition { get; set; }
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