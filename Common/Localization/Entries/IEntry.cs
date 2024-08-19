using Common.Localization.Providers;

namespace Common.Localization.Entries;

public interface IEntry
{
    virtual bool CanGet() => true;
    string Get(ILocalizationProvider provider, params object[] additionalArgs);
    public static IEntry operator +(IEntry first, IEntry second) => new EntryString("{0}{1}", first, second);
    public static IEntry operator +(IEntry first, string second) => new EntryString("{0}{1}", first, second);
}

public abstract class EntryBase : IEntry
{
    public virtual bool CanGet() => true;
    public abstract string Get(ILocalizationProvider provider, params object[] additionalArgs);
    public static IEntry operator +(EntryBase first, IEntry second) => new EntryString("{0}{1}", first, second);
    public static IEntry operator +(EntryBase first, string second) => new EntryString("{0}{1}", first, second);
}

public static class EntryExtensions
{
    public static IEntry WithArg(this IEntry entry, params object[] arguments)
    {
        return new EntryFormatted(entry, arguments);
    }

    public static string Resolve(this ILocalizationProvider localizationProvider, IEntry entry)
        => entry.Get(localizationProvider);

    public static IEntry ToEntry(this string text)
        => new EntryLocalized(text);
}