using System;
using System.Collections.Generic;
using System.Linq;
using Lavalink4NET.Decoding;
using Lavalink4NET.Player;
using LiteDB;

namespace Bot.Utilities.Music {
    public class SpotifyTrackAssociation {
        [Obsolete("This constructor for database engine")]
        public SpotifyTrackAssociation() { }

        public SpotifyTrackAssociation(string spotifyTrackId, string defaultAssociationIdentifier) {
            SpotifyTrackId = spotifyTrackId;
            Associations.Add(new TrackAssociationData(defaultAssociationIdentifier, 0));
        }

        [BsonId] public string SpotifyTrackId { get; set; } = null!;

        public List<TrackAssociationData> Associations { get; set; } = new List<TrackAssociationData>();

        public TrackAssociationData GetBestAssociation() {
            return Associations.Select(data => (data.Score, data)).Max().data;
        }

        public class TrackAssociationData {
            [Obsolete("This constructor for database engine")]
            public TrackAssociationData() { }

            public TrackAssociationData(string identifier, ulong authorId) {
                Identifier = identifier;
                AuthorId = authorId;
            }

            public ulong AuthorId { get; set; }

            public List<ulong> UpvotedUsers { get; set; } = new List<ulong>();
            public List<ulong> DownvotedUsers { get; set; } = new List<ulong>();

            public string Identifier { get; set; } = null!;

            [BsonIgnore]
            public LavalinkTrack Association {
                get => TrackDecoder.DecodeTrack(Identifier);
                set => Identifier = value.Identifier;
            }

            public int Score => (AuthorId == 0 ? 0 : 2) + UpvotedUsers.Count - DownvotedUsers.Count;
        }
    }
}