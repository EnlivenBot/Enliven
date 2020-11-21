using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using Common.Music;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming

// ReSharper disable CollectionNeverUpdated.Global

namespace Common.Config {
    public class GlobalConfig {
        // ReSharper disable once InconsistentNaming
        private static readonly Lazy<GlobalConfig> _globalConfig = new Lazy<GlobalConfig>(() => {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "config.json"))) {
                Directory.CreateDirectory("Config");
                File.Move("config.json", Path.Combine("Config", "config.json"));
            }

            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Config", "config.json"))) {
                var config = JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Config", "config.json")));
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Config", "config.json"),
                    JsonConvert.SerializeObject(config, Formatting.Indented));
                return config;
            }
                
            Directory.CreateDirectory("Config");
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Config", "config.json"),
                JsonConvert.SerializeObject(new GlobalConfig(), Formatting.Indented));
            return new GlobalConfig();
        });

        public static readonly GlobalConfig Instance = _globalConfig.Value;

        [JsonPropertyName("Bot Token")] public string BotToken { get; set; } = "Place your token here";

        [JsonPropertyName("Lavalink Nodes")]
        [Description(
            "Not including self node\nExample:\n{\n  \"Password\": \"youshallnotpass\",\n  \"RestUri\": \"http://localhost:8080/\",\n  \"WebSocketUri\": \"ws://localhost:8080/\"\n}")]
        // ReSharper disable once IdentifierTypo
        public List<LavalinkNodeInfo> LavalinkNodes { get; set; } = new List<LavalinkNodeInfo>();

        [JsonPropertyName("Spotify Client ID")]
        public string SpotifyClientID { get; set; } = "Place SpotifyClientID here";

        [JsonPropertyName("Spotify Client Secret")]
        public string SpotifyClientSecret { get; set; } = "Place SpotifyClientSecret here";
    }
}