using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Music;
using Common.Music.Resolvers;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using SpotifyAPI.Web;
#pragma warning disable 1998

#pragma warning disable 8604
#pragma warning disable 8602

namespace Bot.Music.Spotify;

public class SpotifyMusicResolver : IMusicResolver, ISpotifyAssociationCreator {
    private readonly LavalinkMusicResolver _lavalinkMusicResolver;
    private readonly ISpotifyAssociationProvider _spotifyAssociationProvider;
    private readonly SpotifyClientResolver _spotifyClientResolver;

    public SpotifyMusicResolver(ISpotifyAssociationProvider spotifyAssociationProvider, SpotifyClientResolver spotifyClientResolver, LavalinkMusicResolver lavalinkMusicResolver) {
        _spotifyClientResolver = spotifyClientResolver;
        _lavalinkMusicResolver = lavalinkMusicResolver;
        _spotifyAssociationProvider = spotifyAssociationProvider;
    }

    public async Task<IEnumerable<LavalinkTrack>> Resolve(LavalinkCluster cluster, string query) {
        var url = new SpotifyUrl(query);

        if (!url.IsValid || await _spotifyClientResolver.GetSpotify() is not { } client) return Array.Empty<LavalinkTrack>();
        return await Resolve(url, cluster, client);
    }

    public async Task<SpotifyAssociation?> ResolveAssociation(SpotifyTrackWrapper spotifyTrackWrapper, LavalinkCluster lavalinkCluster) {
        var cachedTrack = _spotifyAssociationProvider.Get(spotifyTrackWrapper.Id);
        if (cachedTrack != null) return cachedTrack;

        try {
            var spotifyClient = (await _spotifyClientResolver.GetSpotify())!;
            var trackInfo = await spotifyTrackWrapper.GetTrackInfo(spotifyClient);
            var lavalinkTracks = await _lavalinkMusicResolver.Resolve(lavalinkCluster, trackInfo);
            var spotifyTrackAssociation = _spotifyAssociationProvider.Create(spotifyTrackWrapper.Id, lavalinkTracks.First().Identifier);
            spotifyTrackAssociation.Save();
            return spotifyTrackAssociation;
        }
        catch (Exception) {
            // ignored
        }

        return null;
    }

    public async Task<IEnumerable<LavalinkTrack>> Resolve(SpotifyUrl url, LavalinkCluster cluster, SpotifyClient? spotify = null) {
        try {
            spotify ??= await _spotifyClientResolver.GetSpotify();
            var spotifyTracks = await url.Resolve(spotify);
            var lavalinkTracks = await spotifyTracks
                .Select(async s => {
                        var association = await ResolveAssociation(s, cluster);
                        return association != null ? new SpotifyLavalinkTrack(s, association.GetBestAssociation().Association, spotify) : null;
                    }
                )
                .WhenAllAsync()
                .PipeAsync(tracks => tracks
                    .OfType<LavalinkTrack>()
                );

            return lavalinkTracks;
        }
        catch (Exception) {
            throw new TrackNotFoundException(false);
        }
    }
}