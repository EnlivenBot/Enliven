using System;
using System.IO;
using Newtonsoft.Json;

namespace Common.Config {
    public class EnlivenConfigProvider {
        private readonly string _filePath;
        private EnlivenConfig? _config;
        private object _lockObject = new object();

        public EnlivenConfigProvider(string filePath = "Config/config.json") {
            _filePath = filePath;
        }

        public bool IsConfigExists() {
            return File.Exists(Path.GetFullPath(_filePath));
        }

        public EnlivenConfig Load() {
            lock (_lockObject) {
                if (_config != null) return _config;

                var path = Path.GetFullPath(_filePath);
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
            var path = Path.GetFullPath(_filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(_config, Formatting.Indented));
        }
    }
}