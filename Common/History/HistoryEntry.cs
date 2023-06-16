using System;
using Common.Localization.Entries;
using Common.Localization.Providers;

namespace Common.History {
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class HistoryEntry : IEntry {
        public HistoryEntry(IEntry entry, string? identifier = null) {
            Entry = entry;
            Identifier = identifier;
        }

        public string? Identifier { get; private set; }

        private IEntry Entry { get; set; }

        public virtual string Get(ILocalizationProvider provider, params object[] additionalArgs) {
            return Entry.Get(provider, additionalArgs);
        }

        public event EventHandler? Updated;
        public event EventHandler? Removing;
        public event EventHandler<InsertingEventArgs>? Inserting;

        public void OnEntryUpdated() {
            Updated?.Invoke(this, EventArgs.Empty);
        }

        public void Remove() {
            Removing?.Invoke(this, EventArgs.Empty);
        }

        public void Insert(HistoryEntry entryToInsert, bool insertToStart) {
            Inserting?.Invoke(this, new InsertingEventArgs(entryToInsert, insertToStart));
        }

        public virtual void Update(IEntry? entry = null) {
            if (entry != null) {
                SilentUpdate(entry);
            }
            OnEntryUpdated();
        }

        public virtual void SilentUpdate(IEntry entry) {
            Entry = entry;
        }

        public class InsertingEventArgs : EventArgs {
            public InsertingEventArgs(HistoryEntry entryToInsert, bool insertToStart) {
                EntryToInsert = entryToInsert;
                InsertToStart = insertToStart;
            }

            public HistoryEntry EntryToInsert { get; private set; }
            public bool InsertToStart { get; set; }
        }
    }
}