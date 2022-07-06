using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using YandexMusicResolver.Config;

namespace Common.Config {
    public class GlobalConfig : ConfigBase, IYandexConfig {
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

        /// <summary>
        /// Common lavalink nodes credentials
        /// </summary>
        /// <example>
        /// {  
        ///      "Password": "youshallnotpass",  
        ///      "RestUri": "http://localhost:8080/",  
        ///      "WebSocketUri": "ws://localhost:8080/"
        ///      "Name": "Name will be displayed at player embed"
        ///  }
        /// </example>
        public List<LavalinkNodeInfo> LavalinkNodes { get; set; } = new();

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

        private IWebProxy? _yandexProxy;

        [JsonIgnore]
        public IWebProxy? YandexProxy {
            get => _yandexProxy ?? (YandexProxyAddress != null ? _yandexProxy = new WebProxy(YandexProxyAddress) : null);
            set => _yandexProxy = value;
        }
    }
}