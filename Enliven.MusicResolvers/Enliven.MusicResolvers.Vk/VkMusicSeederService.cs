using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VkNet.Model;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Enliven.MusicResolvers.Vk;

public class VkMusicSeederService {
    private readonly ILogger<VkMusicSeederService> _logger;
    private readonly VkMusicCacheService _vkCacheService;
    private readonly Uri? _apiExternalUrl;
    private Task _ffmpegInitialization = null!;

    public VkMusicSeederService(IConfiguration configuration, VkMusicCacheService vkCacheService,
        ILogger<VkMusicSeederService> logger) {
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

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    private async Task InitializeFfmpegInternal() {
        await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ".local-ffmpeg");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            var executableFiles = Directory.GetFiles(".local-ffmpeg")
                .Where(s => string.IsNullOrEmpty(Path.GetExtension(s)))
                .ToList();
            if (executableFiles.All(s => (File.GetUnixFileMode(s) & UnixFileMode.UserExecute) != 0)) {
                _logger.LogWarning("Trying to chmod ffmpeg binaries");
                foreach (var executableFile in executableFiles)
                    File.SetUnixFileMode(executableFile,
                        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }
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
        if (_vkCacheService.TryAccess(id, out var path))
            return Results.File(Path.GetFullPath(path), "audio/mpeg", $"{id}.mp3", enableRangeProcessing: true);
        throw new FileNotFoundException();
    }
}