using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Common.Config {
    public class ConfigProvider<TConfig> where TConfig : ConfigBase, new() {
        private TConfig? _config;
        private readonly object _lockObject = new object();

        public ConfigProvider(string configPath) {
            ConfigPath = configPath;
        }

        public string ConfigPath { get; set; }
        public string FullConfigPath => Path.GetFullPath(ConfigPath);
        public string ConfigFileName => Path.GetFileNameWithoutExtension(ConfigPath);

        public bool IsConfigExists() {
            return File.Exists(Path.GetFullPath(ConfigPath));
        }

        public TConfig Load() {
            lock (_lockObject) {
                if (_config != null) return _config;

                var path = Path.GetFullPath(ConfigPath);
                if (File.Exists(path)) {
                    var configText = File.ReadAllText(path);
                    _config = JsonConvert.DeserializeObject<TConfig>(configText);
                }
                else {
                    _config = new TConfig();
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
                $"https://gitlab.com/enlivenbot/enliven/-/blob/master/Common/Config/{typeof(TConfig).Name}.cs */\n");

            if (!File.Exists(path) || File.ReadAllText(path) != configText) {
                File.WriteAllText(path, configText);
            }
        }

        public static IEnumerable<ConfigProvider<TConfig>> GetConfigs(string configsFolder) {
            var configsPath = Path.GetFullPath(configsFolder);
            if (!Directory.Exists(configsPath)) throw new DirectoryNotFoundException("Configs directory not exists");
            
            var configFiles = Directory.GetFiles(configsPath, "*.json");
            if (configFiles.Length == 0) throw new FileNotFoundException("Config files does not exists");

            return configFiles.Select(s => new ConfigProvider<TConfig>(s));
        }
    }
}