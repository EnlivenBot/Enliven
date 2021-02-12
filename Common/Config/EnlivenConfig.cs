using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using Common.Music;
using Newtonsoft.Json;
using YandexMusicResolver.Config;
// ReSharper disable CommentTypo

// ReSharper disable InconsistentNaming

// ReSharper disable CollectionNeverUpdated.Global

namespace Common.Config
{
    public class EnlivenConfig : IYandexConfig
    {
        public EnlivenConfig(string? filePath = null)
        {
            FilePath = filePath ?? "Config/config.json";
        }

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

        public string? YandexToken { get; set; }

        public string? YandexLogin { get; set; }

        public string? YandexPassword { get; set; }


        [JsonIgnore] public IWebProxy? YandexProxy { get; set; }

        private bool isLoaded;
        private readonly string FilePath;

        public void Load()
        {
            if (isLoaded) return;
            isLoaded = true;

            var path = Path.GetFullPath(FilePath);
            
            if (File.Exists(path))
            {
                var configText = File.ReadAllText(path);
                var config = JsonConvert.DeserializeObject<EnlivenConfig>(configText);

                BotToken = config.BotToken;
                LavalinkNodes = config.LavalinkNodes;
                SpotifyClientID = config.SpotifyClientID;
                SpotifyClientSecret = config.SpotifyClientSecret;
                
                YandexProxyAddress = config.YandexProxyAddress;
                YandexProxy = YandexProxyAddress == null ? null : new WebProxy(YandexProxyAddress);
                YandexToken = config.YandexToken;
                YandexLogin = config.YandexLogin;
                YandexPassword = config.YandexPassword;
            }
            
            Save();
        }

        public void Save()
        {
            File.WriteAllText(Path.GetFullPath(FilePath), JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}