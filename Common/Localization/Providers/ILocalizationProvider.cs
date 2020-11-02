using System;

namespace Common.Localization.Providers {
    public interface ILocalizationProvider {
        string Get(string id, params object[]? formatArgs);

        event EventHandler? LanguageChanged;
    }
}