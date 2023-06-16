using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Common.Localization.Entries;
using Common.Localization.Providers;

namespace Common.History {
    public class HistoryCollection : IList<HistoryEntry>, IEntry {
        private readonly List<HistoryEntry> _entries = new List<HistoryEntry>();
        private int _firstAffectedIndex;
        private bool _ignoreDuplicateIds;
        private bool _isChanged = true;
        private string? _lastHistory;
        private ILocalizationProvider? _lastProvider;
        private int _maxEntriesCount;
        private int _maxLastHistoryLength;

        public HistoryCollection(int maxLastHistoryLength = int.MaxValue, int maxEntriesCount = int.MaxValue, bool ignoreDuplicateIds = true) {
            _ignoreDuplicateIds = ignoreDuplicateIds;
            _maxEntriesCount = maxEntriesCount;
            _maxLastHistoryLength = maxLastHistoryLength;
        }

        public int MaxEntriesCount {
            get => _maxEntriesCount;
            set {
                _maxEntriesCount = value;
                _entries.RemoveRange(0, (Count - value).Normalize(0, int.MaxValue));
                OnHistoryChanged();
            }
        }

        public int MaxLastHistoryLength {
            get => _maxLastHistoryLength;
            set {
                _maxLastHistoryLength = value;
                OnHistoryChanged();
            }
        }

        [Obsolete("Use GetLastHistory instead")]
        public string Get(ILocalizationProvider provider, params object[] additionalArgs) {
            return GetLastHistory(provider);
        }

        public IEnumerator<HistoryEntry> GetEnumerator() {
            return _entries.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(HistoryEntry item) {
            if (item.Identifier != null && !_ignoreDuplicateIds && _entries.LastOrDefault()?.Identifier == item.Identifier) {
                RemoveAt(Count - 1);
            }

            _entries.Add(item);
            SubscribeItem(item);
            OnHistoryChanged(_entries.Count - 1);
        }

        public void Clear() {
            foreach (var historyEntry in _entries.ToList()) {
                UnsubscribeItem(historyEntry);
            }

            _entries.Clear();
            OnHistoryChanged();
        }

        public bool Contains(HistoryEntry item) {
            return _entries.Contains(item);
        }

        public void CopyTo(HistoryEntry[] array, int arrayIndex) {
            _entries.CopyTo(array, arrayIndex);
        }

        public bool Remove(HistoryEntry item) {
            try {
                var index = IndexOf(item);
                var result = _entries.Remove(item);
                UnsubscribeItem(item);
                OnHistoryChanged(index);
                return result;
            }
            catch (Exception) {
                return false;
            }
        }

        public int Count => _entries.Count;
        public bool IsReadOnly => false;

        public int IndexOf(HistoryEntry item) {
            return _entries.IndexOf(item);
        }

        public void Insert(int index, HistoryEntry item) {
            _entries.Insert(index, item);
            SubscribeItem(item);
            OnHistoryChanged(index);
        }

        public void RemoveAt(int index) {
            UnsubscribeItem(_entries[index]);
            _entries.RemoveAt(index);
            OnHistoryChanged(index);
        }

        public HistoryEntry this[int index] {
            get => _entries[index];
            set {
                UnsubscribeItem(_entries[index]);
                _entries[index] = value;
                SubscribeItem(value);
                OnHistoryChanged(index);
            }
        }

        private readonly ISubject<HistoryCollection> _historyChangedSubject = new Subject<HistoryCollection>();
        public IObservable<HistoryCollection> HistoryChanged => _historyChangedSubject.AsObservable();

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

                var index = _entries.Count - 1;
                for (; index >= 0; index--) {
                    var s = _entries[index].Get(provider);
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
                    if (stringBuilder.Length + final.Length > MaxLastHistoryLength) return false;
                    stringBuilder.Insert(0, final);
                    return true;
                }
            }
        }

        protected virtual void OnHistoryChanged(int? affectedIndex = null) {
            _isChanged = affectedIndex == null || affectedIndex.Value >= _firstAffectedIndex;
            _historyChangedSubject.OnNext(this);
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