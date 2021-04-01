using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reactive;
using System.Reactive.Subjects;
using Common.Music;
using Newtonsoft.Json;
using YandexMusicResolver.Config;

// ReSharper disable CommentTypo

// ReSharper disable InconsistentNaming

// ReSharper disable CollectionNeverUpdated.Global

namespace Common.Config {
    public class EnlivenConfig : IYandexConfig {
        internal EnlivenConfig() { }
        private readonly Subject<Unit> _saveRequest = new Subject<Unit>();
        public string BotToken { get; set; } = "Place your token here";

        // ReSharper disable once IdentifierTypo
        // Example:
        // {
        //      "Password": "youshallnotpass",
        //      "RestUri": "http://localhost:8080/",
        //      "WebSocketUri": "ws://localhost:8080/"
        //  }
        public List<LavalinkNodeInfo> LavalinkNodes { get; set; } = new List<LavalinkNodeInfo>();

        public string SpotifyClientID { get; set; } = "Place SpotifyClientID here";

        public string SpotifyClientSecret { get; set; } = "Place SpotifyClientSecret here";

        public string? YandexProxyAddress { get; set; }

        public IObservable<Unit> SaveRequest => _saveRequest;

        public string? YandexToken { get; set; }

        [JsonIgnore]
        public bool IsTokenValid { get; set; }

        public string? YandexLogin { get; set; }

        public string? YandexPassword { get; set; }


        [JsonIgnore]
        public IWebProxy? YandexProxy { get; set; }

        public void Load() {
            // Loading handled by EnlivenConfigProvider
        }

        void IYandexConfig.Save() {
            _saveRequest.OnNext(Unit.Default);
        }

        [JsonIgnore]
        string? IYandexTokenHolder.YandexToken {
            get => IsTokenValid ? YandexToken : null;
            set {
                YandexToken = value;
                IsTokenValid = true;
            }
        }
    }
}