namespace Bot.Music.Spotify {
    public interface ISpotifyAssociationProvider {
        SpotifyAssociation? Get(string id);
        SpotifyAssociation Create(string spotifyTrackId, string defaultAssociationIdentifier);
    }
}