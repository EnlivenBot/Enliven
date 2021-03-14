using System;
using System.Collections.Concurrent;
using LiteDB;

namespace Bot.Music.Spotify {
    public class SpotifyAssociationProvider : ISpotifyAssociationProvider {
        private ConcurrentDictionary<string, SpotifyAssociation?> _cache = new ConcurrentDictionary<string, SpotifyAssociation>();
        private ILiteCollection<SpotifyAssociation> _associationCollection;
        public SpotifyAssociationProvider(ILiteCollection<SpotifyAssociation> associationCollection) {
            _associationCollection = associationCollection;
        }

        public SpotifyAssociation? Get(string id) {
            if (_cache.TryGetValue(id, out var association)) return association;
            
            association = _associationCollection.FindById(id);
            if (association == null) return association;
            
            _cache.TryAdd(id, association);
            association.SaveRequest.Subscribe(data => _associationCollection.Upsert(association));
            return association;
        }

        public SpotifyAssociation Create(string spotifyTrackId, string defaultAssociationIdentifier) {
            #pragma warning disable 618
            var spotifyAssociation = new SpotifyAssociation(spotifyTrackId, defaultAssociationIdentifier);
            #pragma warning restore 618
            
            _cache.TryAdd(spotifyTrackId, spotifyAssociation);
            spotifyAssociation.SaveRequest.Subscribe(data => _associationCollection.Upsert(data));
            return spotifyAssociation;
        }
    }
}