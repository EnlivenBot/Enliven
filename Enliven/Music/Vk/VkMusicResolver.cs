using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Common.Config;
using Common.Music.Resolvers;
using Lavalink4NET;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VkNet.Abstractions;
using VkNet.Model;

namespace Bot.Music.Vk;

public class VkMusicResolver : MusicResolverBase<VkLavalinkTrack, VkTrackData>
{
    private readonly VkCredentials _credentials;
    private readonly ILogger<VkMusicResolver> _logger;
    private readonly IVkApi _vkApi;
    private readonly VkMusicSeederService _vkSeederService;
    private bool _isAuthAttempted;

    public VkMusicResolver(IVkApi vkApi, IOptions<VkCredentials> credentials, VkMusicSeederService vkSeederService,
        ILogger<VkMusicResolver> logger)
    {
        _vkApi = vkApi;
        _vkSeederService = vkSeederService;
        _credentials = credentials.Value;
        _logger = logger;
        _ = InitializeVkNet();
    }

    public override bool IsAvailable => _vkApi.IsAuthorized && _vkSeederService.IsVkSeedAvailable;

    private async Task InitializeVkNet()
    {
        if (_isAuthAttempted || _vkApi.IsAuthorized) return;
        _isAuthAttempted = true;

        if (string.IsNullOrEmpty(_credentials.AccessToken))
            _logger.LogInformation("VK access token doesn't provided. VK resolving disabled");

        _logger.LogInformation("Trying to authorize in VK, using provided access token");
        try
        {
            await _vkApi.AuthorizeAsync(new ApiAuthParams() { AccessToken = _credentials.AccessToken });
            _logger.LogInformation("VK authorization successful");
        }
        catch (Exception e)
        {
            _logger.LogInformation(e,
                "VK authorization doesn't completed. Probably something wrong with the token. VK resolving disabled");
        }
    }

    public override bool CanResolve(string query) => new VkUrl(query).IsValid;

    public override async ValueTask<TrackLoadResult> Resolve(IAudioService cluster,
        LavalinkApiResolutionScope resolutionScope, string query)
    {
        var vkUrl = new VkUrl(query);
        var audios = await vkUrl.Resolve(_vkApi);
        var vkLavalinkTracks = audios
            .Select(audio => new VkLavalinkTrack(audio, _vkSeederService))
            .Cast<LavalinkTrack>();
        return TrackLoadResult.CreateSearch(vkLavalinkTracks.ToImmutableArray());
    }

    protected override ValueTask<IEncodedTrack> EncodeTrackInternal(VkLavalinkTrack track)
    {
        return new ValueTask<IEncodedTrack>(new VkTrackData(track.Audio.Id, track.Audio.Url.ToString()));
    }

    public override async ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracksInternal(IEnumerable<VkTrackData> tracks)
    {
        var ids = tracks.Select(data => data.Id?.ToString() ?? new VkUrl(data.Url).Id);
        var vkTracks = await _vkApi.Audio.GetByIdAsync(ids);
        return vkTracks
            .Select(audio => new VkLavalinkTrack(audio, _vkSeederService))
            .Cast<LavalinkTrack>()
            .ToImmutableArray();
    }
}