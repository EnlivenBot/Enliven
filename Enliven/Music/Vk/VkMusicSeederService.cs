using System;
using System.IO;
using System.Threading.Tasks;
using Bot.Utilities;
using Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VkNet.Model.Attachments;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Bot.Music.Vk;

public class VkMusicSeederService : IEndpointProvider {
    private readonly ILogger<VkMusicSeederService> _logger;
    private readonly VkMusicCacheService _vkCacheService;
    private Uri _apiExternalUrl;
    private Task _ffmpegInitialization = null!;
    public VkMusicSeederService(IConfiguration configuration, VkMusicCacheService vkCacheService, ILogger<VkMusicSeederService> logger) {
        _vkCacheService = vkCacheService;
        _logger = logger;
        var apiUrl = configuration.GetValue<string>("ApiExternalUrl");
        IsVkSeedAvailable = !string.IsNullOrWhiteSpace(apiUrl);
        if (!IsVkSeedAvailable) {
            _logger.LogWarning("Since ApiExternalUrl was not set, VK resolving not available");
            return;
        }
        InitializeFfmpeg();
        _apiExternalUrl = new Uri(apiUrl).Append("vk/audio/");
    }

    public bool IsVkSeedAvailable { get; }

    /// <inheritdoc />
    public Task ConfigureEndpoints(WebApplication app) {
        app.MapGet("/vk/audio/{id}", GetMp3Handler);

        return Task.CompletedTask;
    }

    private void InitializeFfmpeg() {
        _logger.LogInformation("Starting FFMPEG downloading");
        _ffmpegInitialization = FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ".local-ffmpeg");
        _ffmpegInitialization.ContinueWith(task => {
            _logger.LogCritical(task.Exception?.Flatten(), "FFMPEG downloaded failed");
        }, TaskContinuationOptions.OnlyOnFaulted);
        _ffmpegInitialization.ContinueWith(task => {
            _logger.LogInformation("FFMPEG downloaded");
        }, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    public async Task<Uri> PrepareTrackAndGetUrl(Audio audio) {
        var id = $"{audio.OwnerId}_{audio.Id}";
        if (!_vkCacheService.TryAccess(id, out _)) {
            await _ffmpegInitialization;
            var filePath = _vkCacheService.GetFilePathForKey(id);
            var conversion = await FFmpeg.Conversions.FromSnippet.Convert(audio.Url.ToString(), filePath);
            conversion.AddParameter("-http_persistent false", ParameterPosition.PreInput);
            conversion.AddParameter("-c copy");
            await conversion.Start();

            _vkCacheService.Put(id);
        }

        return _apiExternalUrl.Append(id);
    }

    private IResult GetMp3Handler(string id) {
        if (_vkCacheService.TryAccess(id, out var path)) return Results.File(Path.GetFullPath(path), "audio/mpeg", $"{id}.mp3", enableRangeProcessing: true);
        throw new FileNotFoundException();
    }
}