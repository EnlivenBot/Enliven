using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Newtonsoft.Json;
using YandexMusicResolver.Config;

namespace Common.Config {
    public class ConfigBase {
        private readonly Subject<Unit> _saveRequest = new Subject<Unit>();

        [JsonIgnore]
        public IObservable<Unit> SaveRequest => _saveRequest.AsObservable();

        public void Load() {
            // Loading handled by ConfigProvider
        }
        
        public void Save() {
            _saveRequest.OnNext(Unit.Default);
        }
    }
}