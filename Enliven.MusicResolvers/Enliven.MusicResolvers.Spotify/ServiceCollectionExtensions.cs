using Common.Infrastructure;
using Enliven.MusicResolvers.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Enliven.MusicResolver.Spotify;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddSpotify(this IServiceCollection builder, IConfiguration configuration) {
        builder.ConfigureNamedOptions<SpotifyCredentials>(configuration);
        builder.AddSingleton<IMusicResolver, SpotifyMusicResolver>();

        return builder;
    }
}