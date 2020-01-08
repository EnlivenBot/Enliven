using System;
using System.IO;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Bot.Config {
    internal class GlobalConfig {
        private static readonly Lazy<GlobalConfig> _globalConfig = new Lazy<GlobalConfig>(() => {
            if (File.Exists("config.json"))
                return JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText("config.json"));
            File.WriteAllText("config.json", JsonConvert.SerializeObject(new GlobalConfig(), Formatting.Indented));
            return new GlobalConfig();
        });

        public static GlobalConfig Instance = _globalConfig.Value;

        [JsonPropertyName("Bot Token")] public string BotToken { get; set; } = "Place your token here";
    }
}