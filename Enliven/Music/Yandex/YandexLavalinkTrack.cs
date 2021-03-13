using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Lavalink4NET.Decoding;
using Lavalink4NET.Player;
using YandexMusicResolver.AudioItems;
using YandexMusicResolver.Loaders;

namespace Bot.Music.Yandex {
    public class YandexLavalinkTrack : LavalinkTrack {
        private YandexMusicDirectUrlLoader _directUrlLoader;
        public YandexMusicTrack RelatedYandexTrack;

        public YandexLavalinkTrack(YandexMusicTrack relatedYandexTrack, YandexMusicDirectUrlLoader directUrlLoader)
            : base(TrackDecoder.EncodeTrack(relatedYandexTrack.ToTrackInfo()), relatedYandexTrack.ToTrackInfo()) {
            RelatedYandexTrack = relatedYandexTrack;
            _directUrlLoader = directUrlLoader;
        }

        public override async Task<LavalinkTrack> GetPlayableTrack() {
            var directUrl = await GetDirectUrl(RelatedYandexTrack.Id);
            var newTrackInfo = new LavalinkTrackInfo(Author, Duration, IsLiveStream, IsSeekable, Position, directUrl, Title, directUrl);
            return new LavalinkTrack(TrackDecoder.EncodeTrack(newTrackInfo, StreamProvider.Http, writer => {
                writer.WriteString("mp3");
            }), newTrackInfo);
        }
        
        private static readonly Dictionary<string, string> UrlCache = new Dictionary<string, string>();

        private async Task<string> GetDirectUrl(string id) {
            if (UrlCache.TryGetValue(id, out var url)) {
                var isUrlAccessibleResponse = (HttpWebResponse) await WebRequest.Create(url).GetResponseAsync();
                if (isUrlAccessibleResponse.StatusCode == HttpStatusCode.OK)
                    return url;
            }

            var directUrl = await _directUrlLoader.GetDirectUrl(id);
            UrlCache[id] = directUrl;
            return directUrl;
        }
    }
}