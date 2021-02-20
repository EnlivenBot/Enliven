using System;
using Lavalink4NET.Player;
using YandexMusicResolver.AudioItems;

namespace Bot.Music.Yandex {
    public static class YandexMusicUtilities {
        public static LavalinkTrackInfo ToTrackInfo(this YandexMusicTrack track) {
            return new LavalinkTrackInfo(track.Author, track.Length, false, true, TimeSpan.Zero, track.Uri, track.Title, track.Id);
        }
    }
}