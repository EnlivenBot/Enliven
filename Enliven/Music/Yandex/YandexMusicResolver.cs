using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;
using Common;
using Common.Music.Resolvers;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using YandexMusicResolver;
using YandexMusicResolver.AudioItems;

namespace Bot.Music.Yandex
{
    public class YandexMusicResolver : IMusicResolver
    {
        private YandexClientResolver _clientResolver;

        public YandexMusicResolver(YandexClientResolver clientResolver)
        {
            _clientResolver = clientResolver;
        }

        public async Task<MusicResolveResult> Resolve(LavalinkCluster cluster, string query)
        {
            var yandexMusicMainResolver = await _clientResolver.GetClient();
            var yandexQueryTask = yandexMusicMainResolver.ResolveQuery(query, true, false);
            return new MusicResolveResult(
                async () => await yandexQueryTask != null,
                async () =>
                {
                    var yandexMusicSearchResult = (await yandexQueryTask)!;
                    var tracks = new List<LavalinkTrack>();

                    // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                    switch (yandexMusicSearchResult.Type)
                    {
                        case YandexSearchType.Track:
                            if (yandexMusicSearchResult.Tracks?.Count != 0)
                                tracks.Add(YandexLavalinkTrack.CreateInstance(yandexMusicSearchResult.Tracks!.First(),
                                    yandexMusicMainResolver.DirectUrlLoader));
                            break;
                        case YandexSearchType.Album:
                            if (yandexMusicSearchResult.Albums?.Count != 0)
                                tracks.AddRange(
                                    (await yandexMusicSearchResult.Albums!.First().LoadDataAsync())
                                    .Select(track =>
                                        YandexLavalinkTrack.CreateInstance(track, yandexMusicMainResolver.DirectUrlLoader))
                                );
                            break;
                        case YandexSearchType.Playlist:
                            if (yandexMusicSearchResult.Playlists?.Count != 0)
                                tracks.AddRange(
                                    (await yandexMusicSearchResult.Playlists!.First().LoadDataAsync())
                                    .Select(track =>
                                        YandexLavalinkTrack.CreateInstance(track, yandexMusicMainResolver.DirectUrlLoader))
                                );
                            break;
                    }

                    return tracks;
                }
            );
        }

        public Task OnException(LavalinkCluster cluster, string query, Exception e)
        {
            if (e is YandexMusicException {InnerException: AuthenticationException _}) {
                _clientResolver.SetAuthFailed();
            }
            
            return Task.CompletedTask;
        }
    }
}