using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Common.Music.Resolvers;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Bot.Music.Deezer;

public sealed class DeezerMusicResolver : IMusicResolver
{
    private static readonly Regex DeezerAppStateRegex = new(@"<script>window\.__DZR_APP_STATE__ = (.*)<\/script>");

    private static readonly Regex DeezerLinkRegex =
        new(@"(?:deezer\.com\/\D*\/(?:album|track|playlist)|deezer\.page\.link\/\w*)");

    private static readonly HttpClient HttpClient = new();

    private readonly ILogger<DeezerMusicResolver> _logger;

    public DeezerMusicResolver(ILogger<DeezerMusicResolver> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable => true;
    public bool CanResolve(string query) => DeezerLinkRegex.IsMatch(query);

    public async ValueTask<MusicResolveResult> Resolve(ITrackManager cluster,
        LavalinkApiResolutionScope resolutionScope,
        string query, CancellationToken cancellationToken)
    {
        try
        {
            var pageContent = await HttpClient.GetStringAsync(query);
            var deezerAppStateJson = DeezerAppStateRegex.Match(pageContent).Groups[1].Value;
            var state = JObject.Parse(deezerAppStateJson);

            var trackDatas = state.ContainsKey("SONGS")
                ? state["SONGS"]!["data"]!.ToArray()
                : new[] { state["DATA"] };

            string? playlistTitle = null;
            if (state.TryGetValue("DATA", out var dataToken) && dataToken.Contains("TITLE"))
            {
                playlistTitle = dataToken.Value<string>("TITLE")!;
            }

            var trackLoadResults = trackDatas
                .Select(token => new
                {
                    Title = token!.Value<string>("SNG_TITLE"),
                    Artist = token!.Value<string>("ART_NAME")
                })
                .ToAsyncEnumerable()
                .SelectAwait(async arg => await cluster.LoadTrackAsync($"{arg.Title} {arg.Artist}", TrackSearchMode.YouTube,
                    resolutionScope, cancellationToken))
                .Where(track => track is not null)
                .OfType<LavalinkTrack>();

            return new MusicResolveResult(trackLoadResults, playlistTitle);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to resolve Deezer tracks");
            return TrackLoadResult.CreateError(new TrackException(ExceptionSeverity.Suspicious,
                "Failed to resolve Deezer tracks", e.Message));
        }
    }

    public bool CanEncodeTrack(LavalinkTrack track)
    {
        return false;
    }

    public ValueTask<IEncodedTrack> EncodeTrack(LavalinkTrack track)
    {
        throw new NotSupportedException();
    }

    public bool CanDecodeTrack(IEncodedTrack track)
    {
        return false;
    }

    public ValueTask<IReadOnlyList<LavalinkTrack>> DecodeTracks(params IEncodedTrack[] tracks)
    {
        throw new NotSupportedException();
    }
}