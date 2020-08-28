using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bot.Config.Localization.Entries;
using Bot.Config.Localization.Providers;

namespace Bot.Utilities.History {
    public class HistoryCollection : IList<HistoryEntry>, IEntry {
        private readonly List<HistoryEntry> entries = new List<HistoryEntry>();
        private int _firstAffectedIndex;
        private bool _ignoreDuplicateIds;
        private bool _isChanged = true;
        private string? _lastHistory;
        private ILocalizationProvider? _lastProvider;
        private int _maxEntriesCount;
        private int _maxLastHistoryLenght;

        public HistoryCollection(int maxLastHistoryLenght = int.MaxValue, int maxEntriesCount = int.MaxValue, bool ignoreDuplicateIds = true) {
            _ignoreDuplicateIds = ignoreDuplicateIds;
            _maxEntriesCount = maxEntriesCount;
            _maxLastHistoryLenght = maxLastHistoryLenght;
        }

        public int MaxEntriesCount {
            get => _maxEntriesCount;
            set {
                _maxEntriesCount = value;
                entries.RemoveRange(0, (Count - value).Normalize(0, int.MaxValue));
                OnHistoryChanged();
            }
        }

        public int MaxLastHistoryLenght {
            get => _maxLastHistoryLenght;
            set {
                _maxLastHistoryLenght = value;
                OnHistoryChanged();
            }
        }

        [Obsolete("Use GetLastHistory instead")]
        public string Get(ILocalizationProvider provider, params object[] additionalArgs) {
            return GetLastHistory(provider);
        }

        public IEnumerator<HistoryEntry> GetEnumerator() {
            return entries.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(HistoryEntry item) {
            if (item.Identifier != null && !_ignoreDuplicateIds && entries.LastOrDefault()?.Identifier == item.Identifier) {
                RemoveAt(Count - 1);
            }

            entries.Add(item);
            SubscribeItem(item);
            OnHistoryChanged(entries.Count - 1);
        }

        public void Clear() {
            foreach (var historyEntry in entries.ToList()) {
                UnsubscribeItem(historyEntry);
            }

            entries.Clear();
            OnHistoryChanged();
        }

        public bool Contains(HistoryEntry item) {
            return entries.Contains(item);
        }

        public void CopyTo(HistoryEntry[] array, int arrayIndex) {
            entries.CopyTo(array, arrayIndex);
        }

        public bool Remove(HistoryEntry item) {
            try {
                var index = IndexOf(item);
                var result = entries.Remove(item);
                UnsubscribeItem(item);
                OnHistoryChanged(index);
                return result;
            }
            catch (Exception) {
                return false;
            }
        }

        public int Count => entries.Count;
        public bool IsReadOnly => false;

        public int IndexOf(HistoryEntry item) {
            return entries.IndexOf(item);
        }

        public void Insert(int index, HistoryEntry item) {
            entries.Insert(index, item);
            SubscribeItem(item);
            OnHistoryChanged(index);
        }

        public void RemoveAt(int index) {
            UnsubscribeItem(entries[index]);
            entries.RemoveAt(index);
            OnHistoryChanged(index);
        }

        public HistoryEntry this[int index] {
            get => entries[index];
            set {
                UnsubscribeItem(entries[index]);
                entries[index] = value;
                SubscribeItem(value);
                OnHistoryChanged(index);
            }
        }

        public event EventHandler? HistoryChanged;

        public string GetLastHistory(ILocalizationProvider provider) {
            return GetLastHistory(provider, out _);
        }

        public string GetLastHistory(ILocalizationProvider provider, out bool isChanged) {
            if (_lastHistory != null && _lastProvider == provider && !_isChanged) {
                isChanged = false;
                return _lastHistory;
            }

            var result = BuildHistory();

            _isChanged = false;
            _lastProvider = provider;

            isChanged = _lastHistory != result;
            return _lastHistory = result;

            string BuildHistory() {
                string? lastEntry = null;
                var count = 1;
                var stringBuilder = new StringBuilder();

                var index = entries.Count - 1;
                for (; index >= 0; index--) {
                    var s = entries[index].Get(provider);
                    if (lastEntry == s && !_ignoreDuplicateIds) {
                        count++;
                    }
                    else {
                        if (!AppendEntry()) return stringBuilder.ToString();
                        lastEntry = s;
                        count = 1;
                    }
                }

                _firstAffectedIndex = index;
                AppendEntry();
                return stringBuilder.ToString();

                bool AppendEntry() {
                    if (lastEntry == null) return true;
                    var final = count > 1 ? $"{lastEntry} (**x{count}**){Environment.NewLine}" : $"{lastEntry}{Environment.NewLine}";
                    if (stringBuilder.Length + final.Length > MaxLastHistoryLenght) return false;
                    stringBuilder.Insert(0, final);
                    return true;
                }
            }
        }

        protected virtual void OnHistoryChanged(int? affectedIndex = null) {
            _isChanged = affectedIndex == null || affectedIndex.Value >= _firstAffectedIndex;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SubscribeItem(HistoryEntry entry) {
            entry.Updated += EntryOnUpdated;
            entry.Removing += EntryOnRemoving;
            entry.Inserting += EntryOnInserting;
        }

        private void EntryOnInserting(object? sender, HistoryEntry.InsertingEventArgs e) {
            if (e.InsertToStart) {
                Add(e.EntryToInsert);
            }
            else {
                Insert(IndexOf((HistoryEntry) sender!) + 1, e.EntryToInsert);
            }
        }

        private void EntryOnRemoving(object? sender, EventArgs e) {
            Remove((HistoryEntry) sender!);
        }

        private void EntryOnUpdated(object? sender, EventArgs e) {
            OnHistoryChanged(IndexOf((HistoryEntry) sender!));
        }

        private void UnsubscribeItem(HistoryEntry entry) {
            entry.Updated -= EntryOnUpdated;
            entry.Removing -= EntryOnRemoving;
            entry.Inserting -= EntryOnInserting;
        }
    }
}