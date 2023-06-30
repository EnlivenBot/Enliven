using System;

namespace Common.Localization.Providers {
    public interface ILocalizationProvider {
        public IObservable<ILocalizationProvider> LanguageChanged { get; }
        string Get(string id, params object[]? formatArgs);
    }
}