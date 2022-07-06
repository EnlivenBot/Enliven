using System;
using System.Reactive.Subjects;

namespace Common.Localization.Providers {
    public interface ILocalizationProvider {
        string Get(string id, params object[]? formatArgs);

        public IObservable<ILocalizationProvider> LanguageChanged { get; }
    }
}