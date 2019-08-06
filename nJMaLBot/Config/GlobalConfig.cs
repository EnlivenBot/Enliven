using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Bot.Config
{
    class GlobalConfig
    {
        public static GlobalConfig LoadConfig() {
            if (File.Exists("config.json"))
                return JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText("config.json"));
            File.WriteAllText("config.json", JsonConvert.SerializeObject(new GlobalConfig(), Formatting.Indented));
            return new GlobalConfig();
        }

        public void SaveConfig() { File.ReadAllText(JsonConvert.SerializeObject(this, Formatting.Indented)); }

        [JsonPropertyName("Bot Token")] public string BotToken { get; set; }
    }
}
