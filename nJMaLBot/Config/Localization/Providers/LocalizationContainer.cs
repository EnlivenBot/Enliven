using System;

namespace Bot.Config.Localization.Providers {
    public class LocalizationContainer : ILocalizationProvider {
        private ILocalizationProvider _provider = null!;

        public LocalizationContainer(ILocalizationProvider provider) {
            Provider = provider;
        }

        public ILocalizationProvider Provider {
            get => _provider;
            set {
                if (_provider != null) _provider.LanguageChanged -= ProviderLanguageChanged;
                _provider = value ?? throw new ArgumentNullException(nameof(value), $"Provider in {nameof(LocalizationContainer)} can not be null");
                _provider.LanguageChanged += ProviderLanguageChanged;
            }
        }

        public string Get(string id, params object[]? formatArgs) {
            return Provider.Get(id, formatArgs);
        }

        public event EventHandler? LanguageChanged;

        private void ProviderLanguageChanged(object? sender, EventArgs e) {
            OnLanguageChanged();
        }

        protected virtual void OnLanguageChanged() {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}