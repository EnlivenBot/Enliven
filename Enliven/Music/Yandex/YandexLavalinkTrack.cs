using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Common;
using Common.Music.Tracks;
using Lavalink4NET.Decoding;
using Lavalink4NET.Player;
using YandexMusicResolver.AudioItems;
using YandexMusicResolver.Loaders;

namespace Bot.Music.Yandex {
    public class YandexLavalinkTrack : LavalinkTrack, ITrackHasArtwork {
        private static readonly HttpClient HttpClient = new(); 
        private IYandexMusicDirectUrlLoader _directUrlLoader;
        public YandexMusicTrack RelatedYandexTrack { get; }

        private YandexLavalinkTrack(YandexMusicTrack relatedYandexTrack, IYandexMusicDirectUrlLoader directUrlLoader, string identifier, LavalinkTrackInfo trackInformation)
            : base(identifier, trackInformation) {
            RelatedYandexTrack = relatedYandexTrack;
            _directUrlLoader = directUrlLoader;
        }

        public static YandexLavalinkTrack CreateInstance(YandexMusicTrack relatedYandexTrack, IYandexMusicDirectUrlLoader directUrlLoader) {
            var lavalinkTrackInfo = new LavalinkTrackInfo() {
                Author = relatedYandexTrack.Author, Duration = relatedYandexTrack.Length, IsLiveStream = false, IsSeekable = true, 
                Position = TimeSpan.Zero, Uri = relatedYandexTrack.Uri?.Pipe(s => new Uri(s)), Title = relatedYandexTrack.Title, TrackIdentifier = relatedYandexTrack.Id
            };
            return new YandexLavalinkTrack(relatedYandexTrack, directUrlLoader, TrackEncoder.Encode(lavalinkTrackInfo), lavalinkTrackInfo);
        }

        public override async ValueTask<LavalinkTrack> GetPlayableTrack() {
            var directUrl = await GetDirectUrl(RelatedYandexTrack.Id);
            var lavalinkTrackInfo = new LavalinkTrackInfo() {
                Author = Author, Duration = Duration, IsLiveStream = IsLiveStream, IsSeekable = IsSeekable, 
                Position = Position, Uri = new Uri(directUrl), Title = Title, TrackIdentifier = directUrl, SourceName = "http"
            };
            return lavalinkTrackInfo.CreateTrack();
        }
        
        private static readonly Dictionary<string, string> UrlCache = new Dictionary<string, string>();
        private async Task<string> GetDirectUrl(string id) {
            if (UrlCache.TryGetValue(id, out var url)) {
                var isUrlAccessibleResponse = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                if (isUrlAccessibleResponse.IsSuccessStatusCode)
                    return url;
            }

            var directUrl = await _directUrlLoader.GetDirectUrl(id);
            UrlCache[id] = directUrl;
            return directUrl;
        }
        
        public ValueTask<Uri?> GetArtwork() {
            var artworkUri = RelatedYandexTrack.ArtworkUrl?.Pipe(s => new Uri(s));
            return ValueTask.FromResult(artworkUri);
        }
    }
}