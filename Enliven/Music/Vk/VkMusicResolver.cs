using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common.Config;
using Common.Music.Resolvers;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VkNet.Abstractions;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Utils;

namespace Bot.Music.Vk;

public class VkMusicResolver : IMusicResolver {
    private static readonly Regex VkTrackRegex = new(@"https:\/\/vk\.com\/audio(-?\d*_\d*)", RegexOptions.Compiled);
    private static readonly Regex VkAlbumRegex = new(@"https:\/\/vk\.com\/music\/album\/(-?\d*)_(\d*)_(\S*)", RegexOptions.Compiled);
    private static readonly Regex VkUserRegex = new(@"https:\/\/vk\.com\/audios(-?\d*)", RegexOptions.Compiled);
    private readonly VkCredentials _credentials;
    private readonly ILogger<VkMusicResolver> _logger;
    private readonly IVkApi _vkApi;
    private readonly VkMusicSeederService _vkSeederService;
    private bool _isAuthAttempted;
    public VkMusicResolver(IVkApi vkApi, IOptions<VkCredentials> credentials, VkMusicSeederService vkSeederService, ILogger<VkMusicResolver> logger) {
        _vkApi = vkApi;
        _vkSeederService = vkSeederService;
        _credentials = credentials.Value;
        _logger = logger;
        _ = InitializeVkNet();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LavalinkTrack>> Resolve(LavalinkCluster cluster, string query) {
        if (!_vkApi.IsAuthorized || !_vkSeederService.IsVkSeedAvailable) return Array.Empty<LavalinkTrack>();

        var trackMatch = VkTrackRegex.Match(query);
        if (trackMatch.Success) {
            var audios = await _vkApi.Audio.GetByIdAsync(new[] { trackMatch.Groups[1].Value });

            return audios.Select(audio => VkLavalinkTrack.CreateInstance(audio, _vkSeederService));
        }

        var albumMatch = VkAlbumRegex.Match(query);
        if (albumMatch.Success) {
            var audios = await _vkApi.CallAsync<VkCollection<Audio>>("audio.get", new VkParameters() {
                {
                    "owner_id", long.Parse(albumMatch.Groups[1].Value)
                }, {
                    "playlist_id", long.Parse(albumMatch.Groups[2].Value)
                }, {
                    "access_key", albumMatch.Groups[3].Value
                }
            });

            return audios.Select(audio => VkLavalinkTrack.CreateInstance(audio, _vkSeederService));
        }

        var userMatch = VkUserRegex.Match(query);
        if (userMatch.Success) {
            var audios = await _vkApi.CallAsync<VkCollection<Audio>>("audio.get", new VkParameters() {
                {
                    "owner_id", long.Parse(userMatch.Groups[1].Value)
                }
            });

            return audios.Select(audio => VkLavalinkTrack.CreateInstance(audio, _vkSeederService));
        }

        return Array.Empty<LavalinkTrack>();
    }

    private async Task InitializeVkNet() {
        if (_isAuthAttempted || _vkApi.IsAuthorized) return;
        _isAuthAttempted = true;

        if (string.IsNullOrEmpty(_credentials.AccessToken)) _logger.LogInformation("VK access token doesn't provided. VK resolving disabled");

        _logger.LogInformation("Trying to authorize in VK, using provided access token");
        try {
            await _vkApi.AuthorizeAsync(new ApiAuthParams() { AccessToken = _credentials.AccessToken });
            _logger.LogInformation("VK authorization successful");
        }
        catch (Exception e) {
            _logger.LogInformation(e, "VK authorization doesn't completed. Probably something wrong with the token. VK resolving disabled");
        }
    }
}