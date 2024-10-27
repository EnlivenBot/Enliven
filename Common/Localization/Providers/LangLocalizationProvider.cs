using System;

namespace Common.Localization.Providers;

public class LangLocalizationProvider : ILocalizationProvider
{
    private readonly string _lang;

    public LangLocalizationProvider(string lang)
    {
        _lang = lang;
    }

    public static LangLocalizationProvider EnglishLocalizationProvider { get; } = new("en");

    public string Get(string id, params object[]? formatArgs)
    {
        return LocalizationManager.Get(_lang, id, formatArgs);
    }

    public IObservable<ILocalizationProvider>? LanguageChanged { get; } = null;
}