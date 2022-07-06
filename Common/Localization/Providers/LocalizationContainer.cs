using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Common.Localization.Providers {
    public class LocalizationContainer : ILocalizationProvider {
        private ILocalizationProvider? _provider;
        private IDisposable? _languageChangedSubscriber;

        public LocalizationContainer(ILocalizationProvider provider) {
            Provider = provider;
        }

        public ILocalizationProvider Provider {
            get => _provider!;
            set {
                _provider = value ?? throw new ArgumentNullException(nameof(value), $"Provider in {nameof(LocalizationContainer)} can not be null");
                _languageChangedSubscriber?.Dispose();
                _languageChangedSubscriber = _provider.LanguageChanged?.Subscribe(_languageChanged);
            }
        }

        public string Get(string id, params object[]? formatArgs) {
            return Provider.Get(id, formatArgs);
        }
        
        private readonly ISubject<ILocalizationProvider> _languageChanged = new Subject<ILocalizationProvider>();
        public IObservable<ILocalizationProvider> LanguageChanged => _languageChanged.AsObservable();
    }
}