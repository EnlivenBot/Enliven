using System;
using System.Reactive.Subjects;

namespace Common.Localization.Providers {
    public class LocalizationContainer : ILocalizationProvider {
        private ILocalizationProvider? _provider;
        private IDisposable? _languageChangedSubscriber;

        public LocalizationContainer(ILocalizationProvider provider) {
            LanguageChanged = new Subject<ILocalizationProvider>();
            Provider = provider;
        }

        public ILocalizationProvider Provider {
            get => _provider!;
            set {
                _languageChangedSubscriber?.Dispose();
                _provider = value ?? throw new ArgumentNullException(nameof(value), $"Provider in {nameof(LocalizationContainer)} can not be null");
                _languageChangedSubscriber = _provider.LanguageChanged?.Subscribe(LanguageChanged);
            }
        }

        public string Get(string id, params object[]? formatArgs) {
            return Provider.Get(id, formatArgs);
        }

        public ISubject<ILocalizationProvider> LanguageChanged { get; }
    }
}