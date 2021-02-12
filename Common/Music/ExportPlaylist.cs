using System;
using System.Collections.Generic;
using Common.Config;
using LiteDB;

namespace Common.Music {
    public interface IPlaylistProvider {
        StoredPlaylist StorePlaylist(ExportPlaylist exportPlaylist, UserLink authorId);
        StoredPlaylist StorePlaylist(ExportPlaylist exportPlaylist, string id, UserLink authorId);
        StoredPlaylist? Get(string id);
    }

    public class PlaylistProvider : IPlaylistProvider {
        private ILiteCollection<StoredPlaylist> _liteCollection;
        public PlaylistProvider(ILiteCollection<StoredPlaylist> liteCollection) {
            _liteCollection = liteCollection;
        }
        
        public StoredPlaylist StorePlaylist(ExportPlaylist exportPlaylist, UserLink authorId) {
            return StorePlaylist(exportPlaylist, ObjectId.NewObjectId().ToString(), authorId);
        }
        
        public StoredPlaylist StorePlaylist(ExportPlaylist exportPlaylist, string id, UserLink authorId) {
            var storedPlaylist = new StoredPlaylist {
                Tracks = exportPlaylist.Tracks, TrackIndex = exportPlaylist.TrackIndex, TrackPosition = exportPlaylist.TrackPosition,
                Id = id, Author = authorId, CreationTime = DateTime.Now
            };
            
            _liteCollection.Upsert(storedPlaylist);

            return storedPlaylist;
        }

        public StoredPlaylist? Get(string id) {
            return _liteCollection.FindById(id);
        }
    }
    public class ExportPlaylist {
        public List<string> Tracks { get; set; } = new List<string>();
        public int TrackIndex { get; set; } = -1;
        public TimeSpan? TrackPosition { get; set; }
    }

    public class StoredPlaylist : ExportPlaylist {
        [BsonId] public string Id { get; set; } = null!;
        public DateTime CreationTime { get; set; }
        public UserLink Author { get; set; } = null!;
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