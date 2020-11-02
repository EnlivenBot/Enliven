using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Config;
using Lavalink4NET.Decoding;
using Lavalink4NET.Player;
using LiteDB;
using SpotifyAPI.Web;

namespace Bot.Utilities.Music {
    public class SpotifyTrackAssociation {
        private static readonly ILiteCollection<SpotifyTrackAssociation> SpotifyAssociations =
            Database.LiteDatabase.GetCollection<SpotifyTrackAssociation>(@"SpotifyAssociations");
        [Obsolete("This constructor for database engine")]
        public SpotifyTrackAssociation() { }

        public SpotifyTrackAssociation(string spotifyTrackId, string defaultAssociationIdentifier) {
            SpotifyTrackId = spotifyTrackId;
            Associations.Add(new TrackAssociationData(defaultAssociationIdentifier, UserLink.Current));
        }

        [BsonId] public string SpotifyTrackId { get; set; } = null!;

        public List<TrackAssociationData> Associations { get; set; } = new List<TrackAssociationData>();

        public TrackAssociationData GetBestAssociation() {
            return Associations.Select(data => (data.Score, data)).Max().data;
        }

        public void Save() {
            SpotifyAssociations.Upsert(this);
        }

        public static bool TryGet(string id, out SpotifyTrackAssociation association) {
            association = default;
            try {
                association = SpotifyAssociations.FindById(id);
                return association != null;
            }
            catch (Exception) {
                return false;
            }
        }

        public class TrackAssociationData {
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
        }
    }

    public class SpotifyTrack {
        private FullTrack? _track;
        private string? _trackInfo;

        public SpotifyTrack(string id, FullTrack? track = null) {
            _track = track;
            Id = id;
        }
        
        public SpotifyTrack(SimpleTrack track) {
            _trackInfo = $"{track.Name} - {track.Artists[0].Name}";
            Id = track.Id;
        }

        public string Id { get; private set; }

        public async Task<FullTrack> GetFullTrack() {
            return _track ??= await (await SpotifyMusicResolver.SpotifyClient)!.Tracks.Get(Id);
        }
        
        public async Task<string> GetTrackInfo() {
            if (_trackInfo != null)
                return _trackInfo;
            var fullTrack = await GetFullTrack();
            return _trackInfo = $"{fullTrack.Name} - {fullTrack.Artists[0].Name}";
        }
    }
}