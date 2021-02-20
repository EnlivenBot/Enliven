using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Music.Resolvers;
using Lavalink4NET.Cluster;
using Lavalink4NET.Player;
using YandexMusicResolver;
using YandexMusicResolver.AudioItems;

namespace Bot.Music.Yandex {
    public class YandexMusicResolver : IMusicResolver {
        private YandexMusicMainResolver _resolver;

        public YandexMusicResolver(YandexMusicMainResolver resolver) {
            _resolver = resolver;
        }

        public Task<MusicResolveResult> Resolve(LavalinkCluster cluster, string query) {
            var yandexQueryTask = _resolver.ResolveQuery(query, true, false);
            return Task.FromResult(
                new MusicResolveResult(
                    async () => await yandexQueryTask != null,
                    async () => {
                        var yandexMusicSearchResult = (await yandexQueryTask)!;
                        var tracks = new List<LavalinkTrack>();

                        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                        switch (yandexMusicSearchResult.Type) {
                            case YandexSearchType.Track:
                                if (yandexMusicSearchResult.Tracks?.Count != 0)
                                    tracks.Add(new YandexLavalinkTrack(yandexMusicSearchResult.Tracks!.First(), _resolver.DirectUrlLoader));
                                break;
                            case YandexSearchType.Album:
                                if (yandexMusicSearchResult.Albums?.Count != 0)
                                    tracks.AddRange(
                                        (await yandexMusicSearchResult.Albums!.First().LoadDataAsync())
                                       .Select(track => new YandexLavalinkTrack(track, _resolver.DirectUrlLoader))
                                    );
                                break;
                            case YandexSearchType.Playlist:
                                if (yandexMusicSearchResult.Playlists?.Count != 0)
                                    tracks.AddRange(
                                        (await yandexMusicSearchResult.Playlists!.First().LoadDataAsync())
                                       .Select(track => new YandexLavalinkTrack(track, _resolver.DirectUrlLoader))
                                    );
                                break;
                        }

                        return tracks;
                    }));
        }
    }
}