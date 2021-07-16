using Common.Localization.Providers;

namespace Common.Localization.Entries {
    public interface IEntry {
        string Get(ILocalizationProvider provider, params object[] additionalArgs);
        public static IEntry operator +(IEntry first, IEntry second) => new EntryString("{0}{1}", first, second);
        public static IEntry operator +(IEntry first, string second) => new EntryString("{0}{1}", first, second);
    }

    public abstract class EntryBase : IEntry {
        public abstract string Get(ILocalizationProvider provider, params object[] additionalArgs);
        public static IEntry operator +(EntryBase first, IEntry second) => new EntryString("{0}{1}", first, second);
        public static IEntry operator +(EntryBase first, string second) => new EntryString("{0}{1}", first, second);
    }
}