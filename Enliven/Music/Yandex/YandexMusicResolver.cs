using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Music.Resolvers;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using YandexMusicResolver;

namespace Bot.Music.Yandex {
    public class YandexMusicResolver : IMusicResolver {
        private IYandexMusicMainResolver _musicMainResolver;

        public YandexMusicResolver(IYandexMusicMainResolver musicMainResolver) {
            _musicMainResolver = musicMainResolver;
        }

        public async Task<MusicResolveResult> Resolve(LavalinkCluster cluster, string query) {
            var yandexQueryTask = _musicMainResolver.ResolveQuery(query, true, false);
            return new MusicResolveResult(
                async () => await yandexQueryTask != null,
                async () => {
                    var yandexMusicSearchResult = (await yandexQueryTask)!;
                    var tracks = new List<LavalinkTrack>();

                    // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                    switch (yandexMusicSearchResult.Type) {
                        case YandexSearchType.Track:
                            if (yandexMusicSearchResult.Tracks?.Count != 0)
                                tracks.Add(YandexLavalinkTrack.CreateInstance(yandexMusicSearchResult.Tracks!.First(),
                                    _musicMainResolver.DirectUrlLoader));
                            break;
                        case YandexSearchType.Album:
                            if (yandexMusicSearchResult.Albums?.Count != 0)
                                tracks.AddRange(
                                    (await yandexMusicSearchResult.Albums!.First().LoadDataAsync())
                                    .Select(track => YandexLavalinkTrack.CreateInstance(track, _musicMainResolver.DirectUrlLoader))
                                );
                            break;
                        case YandexSearchType.Playlist:
                            if (yandexMusicSearchResult.Playlists?.Count != 0)
                                tracks.AddRange(
                                    (await yandexMusicSearchResult.Playlists!.First().LoadDataAsync())
                                    .Select(track => YandexLavalinkTrack.CreateInstance(track, _musicMainResolver.DirectUrlLoader))
                                );
                            break;
                    }

                    return tracks;
                }
            );
        }

        public Task OnException(LavalinkCluster cluster, string query, Exception e) {
            return Task.CompletedTask;
        }
    }
}