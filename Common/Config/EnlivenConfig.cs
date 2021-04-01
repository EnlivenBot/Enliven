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
        #region Stored properties

        /// <summary>
        /// Your Discord bot token
        /// </summary>
        public string BotToken { get; set; } = "Place your token here";

        /// <summary>
        /// Lavalink nodes credentials
        /// </summary>
        /// <example>
        /// {  
        ///      "Password": "youshallnotpass",  
        ///      "RestUri": "http://localhost:8080/",  
        ///      "WebSocketUri": "ws://localhost:8080/"
        ///      "Name": "Name will be displayed at player embed"
        ///  }
        /// </example>
        public List<LavalinkNodeInfo> LavalinkNodes { get; set; } = new List<LavalinkNodeInfo>();

        /// <summary>
        /// Spotify client ID for resolving spotify related things. Leave null for disabling spotify integration
        /// Obtain at https://developer.spotify.com/dashboard/
        /// </summary>
        /// <seealso cref="SpotifyClientSecret"/>
        public string? SpotifyClientID { get; set; }

        /// <summary>
        /// Spotify client secret for resolving spotify related things. Leave null for disabling spotify integration
        /// Obtain at https://developer.spotify.com/dashboard/
        /// </summary>
        /// <seealso cref="SpotifyClientID"/>
        public string? SpotifyClientSecret { get; set; }

        /// <summary>
        /// Proxy address for resolving YandexMusic related things. Leave null for working without proxy
        /// </summary>
        /// <remarks>
        /// When using authorization or being in the CIS, everything should work without a proxy
        /// </remarks>
        public string? YandexProxyAddress { get; set; }

        /// <summary>
        /// Yandex account access token for getting full YandexMusic tracks
        /// </summary>
        /// <remarks>
        /// Token can be automatically updated (or retrieved) if <see cref="YandexLogin"/> and <see cref="YandexPassword"/> are set
        /// </remarks>
        public string? YandexToken { get; set; }

        /// <summary>
        /// Yandex account credentials for actumatically retrieving <see cref="YandexToken"/>
        /// </summary>
        /// <seealso cref="YandexPassword"/>
        public string? YandexLogin { get; set; }

        /// <summary>
        /// Yandex account credentials for actumatically retrieving <see cref="YandexToken"/>
        /// </summary>
        /// <seealso cref="YandexLogin"/>
        public string? YandexPassword { get; set; }

        #endregion


        #region Other properties

        [JsonIgnore]
        string? IYandexTokenHolder.YandexToken {
            get => IsTokenValid ? YandexToken : null;
            set {
                YandexToken = value;
                IsTokenValid = true;
            }
        }

        [JsonIgnore]
        public bool IsTokenValid { get; set; }

        [JsonIgnore]
        public IWebProxy? YandexProxy {
            get => _yandexProxy ?? (YandexProxyAddress != null ? _yandexProxy = new WebProxy(YandexProxyAddress) : null);
            set => _yandexProxy = value;
        }

        #endregion


        #region System

        private readonly Subject<Unit> _saveRequest = new Subject<Unit>();
        private IWebProxy? _yandexProxy;

        [JsonIgnore]
        public IObservable<Unit> SaveRequest => _saveRequest;

        public void Load() {
            // Loading handled by EnlivenConfigProvider
        }

        void IYandexConfig.Save() {
            _saveRequest.OnNext(Unit.Default);
        }

        #endregion
    }
}