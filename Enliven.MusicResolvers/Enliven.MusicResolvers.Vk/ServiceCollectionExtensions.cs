using Common.Infrastructure;
using Enliven.MusicResolvers.Base;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VkNet;
using VkNet.Abstractions;
using VkNet.AudioBypassService.Extensions;

namespace Enliven.MusicResolvers.Vk;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddVk(this IServiceCollection services, IConfiguration configuration) {
        services.ConfigureNamedOptions<VkCredentials>(configuration);
        services.AddAudioBypass();
        services.AddSingleton<IVkApi, VkApi>();
        services.AddSingleton<VkMusicCacheService>(_ =>
            new VkMusicCacheService(TimeSpan.FromDays(1), ".cache/vkmusic/", "mp3"));
        services.AddSingleton<VkMusicSeederService, VkMusicSeederService>();
        services.AddSingleton<IMusicResolver, VkMusicResolver>();

        return services;
    }

    public static void MapVk(this IEndpointRouteBuilder application) {
        application.MapGet("/vk/audio/{id}", ([FromServices] VkMusicCacheService service, string id) => {
            return service.TryAccess(id, out var path)
                ? Results.File(Path.GetFullPath(path), "audio/mpeg", $"{id}.mp3", enableRangeProcessing: true)
                : throw new FileNotFoundException();
        });
    }
}