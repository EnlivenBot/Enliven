using System;
using Common.Localization.Providers;

namespace Common.Localization.Entries {
    public class EntryContainer : EntryBase {
        public EntryContainer(IEntry entry) {
            SetEntry(entry);
        }

        public EntryContainer(Func<IEntry> entryFunc) {
            SetEntry(entryFunc);
        }

        private IEntry? Entry { get; set; }
        private Func<IEntry>? EntryFunc { get; set; }

        public override string Get(ILocalizationProvider provider, params object[] additionalArgs) {
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