using System;
using System.IO;
using Bot.Utilities;
using Common.Config;
using Common.Music.Resolvers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VkNet;
using VkNet.Abstractions;
using VkNet.AudioBypassService.Extensions;

namespace Bot.Music.Vk;

public static class VkExtensionMethods {
    public static IServiceCollection AddVk(this IServiceCollection services, IConfiguration configuration) {
        services.ConfigureNamedOptions<VkCredentials>(configuration);
        services.AddSingleton<IVkApi>(_ =>
            new VkApi(new ServiceCollection().AddAudioBypass()));
        services.AddSingleton<VkMusicCacheService>(_ =>
            new VkMusicCacheService(TimeSpan.FromDays(1), ".cache/vkmusic/", "mp3"));
        services.AddSingleton<VkMusicSeederService, VkMusicSeederService>();
        services.AddSingleton<IEndpointProvider, VkMusicSeederService>(s =>
            s.GetRequiredService<VkMusicSeederService>());
        services.AddSingleton<IMusicResolver, VkMusicResolver>();

        return services;
    }

    public static void MapVk(this WebApplication application) {
        application.MapGet("/vk/audio/{id}", ([FromServices] VkMusicCacheService service, string id) => {
            if (service.TryAccess(id, out var path))
                return Results.File(Path.GetFullPath(path), "audio/mpeg", $"{id}.mp3", enableRangeProcessing: true);
            throw new FileNotFoundException();
        });
    }
}