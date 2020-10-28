using System;
using Bot.Config.Localization.Providers;

namespace Bot.Config.Localization.Entries {
    public class EntryContainer : IEntry {
        public EntryContainer(IEntry entry) {
            SetEntry(entry);
        }

        public EntryContainer(Func<IEntry> entryFunc) {
            SetEntry(entryFunc);
        }

        private IEntry? Entry { get; set; }
        private Func<IEntry>? EntryFunc { get; set; }

        public string Get(ILocalizationProvider provider, params object[] additionalArgs) {
            return (Entry ?? EntryFunc!()).Get(provider, additionalArgs);
        }

        public void SetEntry(IEntry entry) {
            Entry = entry;
        }

        public void SetEntry(Func<IEntry> entryFunc) {
            EntryFunc = entryFunc;
        }
    }
}