using Common.Infrastructure;
using Enliven.MusicResolvers.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YandexMusicResolver;
using YandexMusicResolver.Config;

namespace Enliven.MusicResolver.YandexMusic;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddYandex(this IServiceCollection services, IConfiguration configuration) {
        services.ConfigureNamedOptions<YandexCredentials>(configuration);
        services.AddYandexMusicResolver();
        services.AddSingleton<IMusicResolver, YandexMusicResolver>();

        return services;
    }
}