using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Bot.Utilities.ObservableDictionaryUtils;

// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable IdentifierTypo
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedParameter.Local
// ReSharper disable InconsistentNaming
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable NotAccessedVariable
// ReSharper disable ConstantNullCoalescingCondition
// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable ConstantConditionalAccessQualifier
#pragma warning disable 8625
#pragma warning disable 8601
#pragma warning disable 8603
#pragma warning disable 8714
#pragma warning disable 8618

namespace Bot.Utilities;

public sealed class ObservableDictionary<TKey, TValue> : IEnumerable<TValue>, ICollection, INotifyCollectionChanged, INotifyPropertyChanged {
    private int _deferCount;
    private List<NotifyCollectionChangedEventArgs> _deferredCollectionChanges;
    private HashSet<string> _deferredPropertyChanges;
    private int _newSnapshotNeeded = 1;
    private volatile ReadOnlyCollection<TValue> _snapshot;

    private object _snapshotLock = new();
    private AVLTree<TKey, TValue> _store;

    public ObservableDictionary() {
        _store = new AVLTree<TKey, TValue>();
    }

    public ObservableDictionary(IDictionary<TKey, TValue> source) {
        _store = new AVLTree<TKey, TValue>();
        foreach (var pair in source)
            BaseAdd(pair);
    }

    public ObservableDictionary(IComparer<TKey> comparer) {
        _store = new AVLTree<TKey, TValue>(comparer);
    }

    public ObservableDictionary(IDictionary<TKey, TValue> source, IComparer<TKey> comparer) {
        _store = new AVLTree<TKey, TValue>(comparer);
        foreach (var pair in source)
            BaseAdd(pair);
    }

    private bool IsDeferred => _deferCount > 0;

    /// <summary>
    /// Gets an immutable snapshot of the collection
    /// </summary>
    public ReadOnlyCollection<TValue> Snapshot {
        get {
            return DoRead(
                () => { return UpdateSnapshot(); });
        }
    }

    public bool IsReadOnly => false;

    public ICollection<TKey> Keys {
        get { return DoRead(() => _store.Keys); }
    }

    public ICollection<TValue> Values {
        get { return DoRead(() => _store.Values); }
    }

    public TValue this[TKey key] {
        get { return DoRead(() => _store[key]); }
        set {
            DoWrite(() => {
                BinaryTreeNode<KeyValuePair<TKey, TValue>> node;
                var index = _store.IndexOfKey(key, out node);
                if (node == null)
                    BaseAdd(new KeyValuePair<TKey, TValue>(key, value));
                else {
                    var oldValue = node.Value.Value;
                    node.Value = new KeyValuePair<TKey, TValue>(key, value);
                    OnCollectionChangedForKey(
                        key,
                        new NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Replace,
                            node.Value.Value,
                            oldValue,
                            index));
                }
            });
        }
    }

    public object SyncRoot => this;

    public bool IsSynchronized => true;

    public int Count {
        get { return DoRead(() => _store.Count); }
    }

    public void CopyTo(Array array, int index) {
        DoRead(() => _store.ToArray().CopyTo(array, index));
    }

    public IEnumerator<TValue> GetEnumerator() {
        return Snapshot.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return Snapshot.GetEnumerator();
    }

    public event NotifyCollectionChangedEventHandler CollectionChanged;

    public event PropertyChangedEventHandler PropertyChanged;

    private void NewSnapshopNeeded() {
        Interlocked.CompareExchange(ref _newSnapshotNeeded, 1, 0);
        OnPropertyChanged("Count");
    }

    public IDisposable DeferRefresh() {
        if (_deferCount++ == 0)
            StartDefer();
        return new DeferHelper(this);
    }

    private void StartDefer() {
        _deferredPropertyChanges ??= new HashSet<string>();
        _deferredCollectionChanges ??= new List<NotifyCollectionChangedEventArgs>();
    }

    private void EndDefer() {
        if (--_deferCount == 0) ProcessDefer();
    }

    private void ProcessDefer() {
        foreach (var key in _deferredPropertyChanges)
            OnPropertyChanged(key);
        _deferredPropertyChanges.Clear();
        foreach (var args in _deferredCollectionChanges)
            OnCollectionChanged(args);
        _deferredCollectionChanges.Clear();
    }

    private ReadOnlyCollection<TValue> UpdateSnapshot() {
        if (_newSnapshotNeeded <= 0) return _snapshot;
        lock (_snapshotLock) {
            if (Interlocked.CompareExchange(ref _newSnapshotNeeded, 0, 1) == 1)
                _snapshot = new ReadOnlyCollection<TValue>(_store.Values.ToList());
        }
        return _snapshot;
    }

    private TResult DoRead<TResult>(Func<TResult> callback) {
        return callback();
    }

    internal void DoRead(Action callback) {
        DoRead<object>(
            () => {
                callback();
                return null;
            });
    }

    private TResult DoWrite<TResult>(Func<TResult> callback) {
        var x = callback();
        NewSnapshopNeeded();
        return x;
    }

    private void DoWrite(Action callback) {
        DoWrite<object>(
            () => {
                callback();
                return null;
            });
    }

    public void Add(KeyValuePair<TKey, TValue> item) {
        DoWrite(() => BaseAdd(item));
    }

    private void BaseAdd(KeyValuePair<TKey, TValue> item) {
        _store.Add(item);
        var index = _store.IndexOfKey(item.Key);
        // Debug.WriteLine(string.Format("Add: {1} - {0}", item.Key, index));
        OnCollectionChangedForKey(item.Key, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item.Value, index));
    }

    internal int IndexOfKey(TKey key, out TValue value) {
        // ReadLock must be obtained first
        BinaryTreeNode<KeyValuePair<TKey, TValue>> node;
        var index = _store.IndexOfKey(key, out node);
        if (index >= 0) {
            value = node.Value.Value;
            return index;
        }

        value = default;
        return -1;
    }

    public void Clear() {
        DoWrite(() => {
            if (_store.Count == 0) return;
            OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    _store.Select(i => i.Value).ToArray(), 0));
            _store.Clear();
        });
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
        DoRead(() => _store.ToArray().CopyTo(array, arrayIndex));
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) {
        return DoWrite(() => BaseRemove(item.Key));
    }

    private bool BaseRemove(TKey key) {
        BinaryTreeNode<KeyValuePair<TKey, TValue>> node;
        var index = _store.IndexOfKey(key, out node);
        if (index >= 0) {
            var value = node.Value;
            _store.Remove(node);
            OnCollectionChangedForKey(value.Key,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove, value.Value,
                    index));
            return true;
        }

        return false;
    }

    public bool ContainsKey(TKey key) {
        return DoRead(() => _store.ContainsKey(key));
    }

    public bool ContainsValue(TValue value) {
        return DoRead(() => _store.Values.Any(v => Comparer<TValue>.Default.Compare(v, value) == 0));
    }

    public void Add(TKey key, TValue value) {
        DoWrite(() => BaseAdd(new KeyValuePair<TKey, TValue>(key, value)));
    }

    public bool Remove(TKey key) {
        return DoWrite(() => BaseRemove(key));
    }

    public bool TryGetValue(TKey key, out TValue value) {
        var innerValue = default(TValue);
        var result = DoRead(() => _store.TryGetValue(key, out innerValue));
        value = innerValue;
        return result;
    }

    private void OnCollectionChangedForKey(TKey key, NotifyCollectionChangedEventArgs args) {
        OnCollectionChanged(args);
    }

    internal void OnCollectionChanged(NotifyCollectionChangedEventArgs args) {
        if (IsDeferred) {
            _deferredCollectionChanges.Add(args);
            return;
        }

        var handler = CollectionChanged;
        handler?.Invoke(this, args);
    }

    private void OnPropertyChanged(string name) {
        if (IsDeferred) {
            _deferredPropertyChanges.Add(name);
            return;
        }

        var handler = PropertyChanged;
        handler?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class DeferHelper : IDisposable {
        private ObservableDictionary<TKey, TValue> _dictionary;

        public DeferHelper(ObservableDictionary<TKey, TValue> dictionary) {
            _dictionary = dictionary;
        }

        public void Dispose() {
            _dictionary.EndDefer();
        }
    }
}