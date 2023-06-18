using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
    private Uri? _apiExternalUrl;
    private Task _ffmpegInitialization = null!;
    public VkMusicSeederService(IConfiguration configuration, VkMusicCacheService vkCacheService, ILogger<VkMusicSeederService> logger) {
        _vkCacheService = vkCacheService;
        _logger = logger;
        var apiUrl = configuration.GetValue<string>("ApiExternalUrl");
        if (string.IsNullOrWhiteSpace(apiUrl)) {
            _logger.LogWarning("Since ApiExternalUrl was not set, VK resolving not available");
            return;
        }
        _apiExternalUrl = new Uri(apiUrl).Append("vk/audio/");
        InitializeFfmpeg();
    }

    public bool IsVkSeedAvailable { get; private set; }

    /// <inheritdoc />
    public Task ConfigureEndpoints(WebApplication app) {
        app.MapGet("/vk/audio/{id}", GetMp3Handler);

        return Task.CompletedTask;
    }

    private void InitializeFfmpeg() {
        _logger.LogInformation("Starting FFMPEG downloading");
        _ffmpegInitialization = InitializeFfmpegInternal();
        _ffmpegInitialization.ContinueWith(task => {
            _logger.LogCritical(task.Exception?.Flatten(), "FFMPEG downloaded failed. VK resolving not available");
        }, TaskContinuationOptions.OnlyOnFaulted);
        _ffmpegInitialization.ContinueWith(task => {
            _logger.LogInformation("FFMPEG downloaded");
            IsVkSeedAvailable = _apiExternalUrl != null;
        }, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    private static async Task InitializeFfmpegInternal() {
        await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ".local-ffmpeg");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            var executableFiles = Directory.GetFiles(".local-ffmpeg")
                .Where(s => string.IsNullOrEmpty(Path.GetExtension(s)));
            foreach (var executableFile in executableFiles) File.SetUnixFileMode(executableFile, UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
        FFmpeg.SetExecutablesPath(".local-ffmpeg");
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

        return _apiExternalUrl!.Append(id);
    }

    private IResult GetMp3Handler(string id) {
        if (_vkCacheService.TryAccess(id, out var path)) return Results.File(Path.GetFullPath(path), "audio/mpeg", $"{id}.mp3", enableRangeProcessing: true);
        throw new FileNotFoundException();
    }
}