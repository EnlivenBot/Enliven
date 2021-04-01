using System;
using System.IO;
using Newtonsoft.Json;

namespace Common.Config {
    public class EnlivenConfigProvider {
        private EnlivenConfig? _config;
        private object _lockObject = new object();

        public EnlivenConfigProvider(string configPath = "Config/config.json") {
            ConfigPath = configPath;
        }

        public string ConfigPath { get; }
        public string FullConfigPath => Path.GetFullPath(ConfigPath);

        public bool IsConfigExists() {
            return File.Exists(Path.GetFullPath(ConfigPath));
        }

        public EnlivenConfig Load() {
            lock (_lockObject) {
                if (_config != null) return _config;

                var path = Path.GetFullPath(ConfigPath);
                if (File.Exists(path)) {
                    var configText = File.ReadAllText(path);
                    _config = JsonConvert.DeserializeObject<EnlivenConfig>(configText);
                }
                else {
                    _config = new EnlivenConfig();
                    Save();
                }

                _config.SaveRequest.Subscribe(x => Save());

                return _config;
            }
        }

        public void Save() {
            var path = Path.GetFullPath(ConfigPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var configText = JsonConvert.SerializeObject(_config, Formatting.Indented);
            configText = configText.Insert(0,
                "/* Properties description can be found here: " +
                "https://gitlab.com/enlivenbot/enliven/-/blob/master/Common/Config/EnlivenConfig.cs */\n");
            File.WriteAllText(path, configText);
        }
    }
}