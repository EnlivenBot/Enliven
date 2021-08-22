using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Common.Config;
using Lavalink4NET.Decoding;
using Lavalink4NET.Player;
using LiteDB;

namespace Bot.Music.Spotify {
    public class SpotifyAssociation {
        [Obsolete("Use SpotifyAssociationProvider")]
        public SpotifyAssociation() { }

        [Obsolete("Use SpotifyAssociationProvider")]
        public SpotifyAssociation(string spotifyTrackId, string defaultAssociationIdentifier) {
            SpotifyTrackId = spotifyTrackId;
            Associations.Add(new TrackAssociationData(defaultAssociationIdentifier, UserLink.Current));
        }

        [BsonId] public string SpotifyTrackId { get; set; } = null!;

        public List<TrackAssociationData> Associations { get; set; } = new List<TrackAssociationData>();

        public TrackAssociationData GetBestAssociation() {
            return Associations.Max();
        }

        [BsonIgnore] private readonly ISubject<SpotifyAssociation> _saveRequest = new Subject<SpotifyAssociation>();
        [BsonIgnore] public IObservable<SpotifyAssociation> SaveRequest => _saveRequest.AsObservable();
        
        public void Save() {
            _saveRequest.OnNext(this);
        }

        public class TrackAssociationData : IComparable<TrackAssociationData> {
            [Obsolete("This constructor for database engine")]
            public TrackAssociationData() { }

            public TrackAssociationData(string identifier, UserLink author) {
                Identifier = identifier;
                Author = author;
            }

            public UserLink Author { get; set; } = null!;

            public List<ulong> UpvotedUsers { get; set; } = new List<ulong>();
            public List<ulong> DownvotedUsers { get; set; } = new List<ulong>();

            public string Identifier { get; set; } = null!;

            [BsonIgnore]
            public LavalinkTrack Association {
                get => TrackDecoder.DecodeTrack(Identifier);
                set => Identifier = value.Identifier;
            }

            public int Score => (Author.IsCurrentUser ? 0 : 2) + UpvotedUsers.Count - DownvotedUsers.Count;

            public void AddVote(ulong userId, bool? isUpvote) {
                UpvotedUsers.Remove(userId);
                DownvotedUsers.Remove(userId);
                switch (isUpvote) {
                    case true:
                        UpvotedUsers.Add(userId);
                        break;
                    case false:
                        DownvotedUsers.Add(userId);
                        break;
                }
            }

            public int CompareTo(TrackAssociationData? other) {
                if (other == null) {
                    return 1;
                }
                return Score - other.Score;
            }
        }
    }
}