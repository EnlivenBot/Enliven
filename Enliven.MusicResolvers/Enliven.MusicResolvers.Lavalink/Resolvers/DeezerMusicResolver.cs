using System.Text.RegularExpressions;
using Enliven.MusicResolvers.Base;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Enliven.MusicResolvers.Lavalink.Resolvers;

public sealed partial class DeezerMusicResolver(ILogger<DeezerMusicResolver> logger) : IMusicResolver {
    private static readonly Regex _deezerAppStateRegex = DeezerAppStateRegex();

    private static readonly Regex _deezerLinkRegex = DeezerLinkRegex();

    private static readonly HttpClient _httpClient = new();

    public bool IsAvailable => true;
    public bool CanResolve(string query) => _deezerLinkRegex.IsMatch(query);

    public async ValueTask<MusicResolveResult> Resolve(ITrackManager cluster,
        LavalinkApiResolutionScope resolutionScope,
        string query, CancellationToken cancellationToken) {
        try {
            var pageContent = await _httpClient.GetStringAsync(query, cancellationToken);
            var deezerAppStateJson = _deezerAppStateRegex.Match(pageContent).Groups[1].Value;
            var state = JObject.Parse(deezerAppStateJson);

            var trackDatas = state.TryGetValue("SONGS", out var value)
                ? value["data"]!.ToArray()
                : [state["DATA"]!];

            string? playlistTitle = null;
            if (state.TryGetValue("DATA", out var dataToken) && dataToken.Contains("TITLE")) {
                playlistTitle = dataToken.Value<string>("TITLE")!;
            }

            var trackLoadResults = trackDatas
                .Select(token => new {
                    Title = token!.Value<string>("SNG_TITLE"),
                    Artist = token!.Value<string>("ART_NAME")
                })
                .ToAsyncEnumerable()
                .SelectAwait(async arg => await cluster.LoadTrackAsync($"{arg.Title} {arg.Artist}",
                    TrackSearchMode.YouTube,
                    resolutionScope, cancellationToken))
                .Where(track => track is not null)
                .OfType<LavalinkTrack>();

            return new MusicResolveResult(trackLoadResults, playlistTitle);
        }
        catch (Exception e) {
            logger.LogError(e, "Failed to resolve Deezer tracks");
            return TrackLoadResult.CreateError(new TrackException(ExceptionSeverity.Suspicious,
                "Failed to resolve Deezer tracks", e.Message));
        }
    }

    public bool CanEncodeTrack(LavalinkTrack track) {
        return false;
    }

    public ValueTask<IEncodedTrack> EncodeTrack(LavalinkTrack track) {
        throw new NotSupportedException();
    }

    public bool CanDecodeTrack(IEncodedTrack track) {
        return false;
    }

    public ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracks(params IEncodedTrack[] tracks) {
        throw new NotSupportedException();
    }

    [GeneratedRegex(@"(?:deezer\.com\/\D*\/(?:album|track|playlist)|deezer\.page\.link\/\w*)")]
    private static partial Regex DeezerLinkRegex();

    [GeneratedRegex(@"<script>window\.__DZR_APP_STATE__ = (.*)<\/script>")]
    private static partial Regex DeezerAppStateRegex();
}