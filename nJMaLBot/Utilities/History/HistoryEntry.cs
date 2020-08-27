using System;
using Bot.Config.Localization.Entries;

namespace Bot.Utilities.History {
    public class HistoryEntry {
        private IEntry _entry = null!;
        
        public string? Identifier { get; private set; }

        public HistoryEntry(IEntry entry, string? identifier = null) {
            Entry = entry;
            Identifier = identifier;
        }

        public event EventHandler? Updated;
        public event EventHandler? Removing;
        public event EventHandler<InsertingEventArgs>? Inserting;

        public IEntry Entry {
            get => _entry;
            set {
                _entry = value;
                OnEntryUpdated();
            }
        }

        protected virtual void OnEntryUpdated() {
            Updated?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void Remove() {
            Removing?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void Insert(HistoryEntry entryToInsert, bool insertToStart) {
            Inserting?.Invoke(this, new InsertingEventArgs(entryToInsert, insertToStart));
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