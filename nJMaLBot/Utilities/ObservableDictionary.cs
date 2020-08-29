using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable IdentifierTypo
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedParameter.Local
// ReSharper disable InconsistentNaming
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable NotAccessedVariable
#pragma warning disable 8625
#pragma warning disable 8601
#pragma warning disable 8603
#pragma warning disable 8714
#pragma warning disable 8618

namespace Bot.Utilities {
    public sealed class ObservableDictionary<TKey, TValue> : IEnumerable<TValue>, ICollection, INotifyCollectionChanged, INotifyPropertyChanged {
        private AVLTree<TKey, TValue> _store;

        private object _snapshotLock = new object();
        private int _newSnapshotNeeded = 1;
        private volatile ReadOnlyCollection<TValue> _snapshot;

        private int _deferCount;
        private HashSet<string> _deferredPropertyChanges;
        private List<NotifyCollectionChangedEventArgs> _deferredCollectionChanges;

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

        private void NewSnapshopNeeded() {
            Interlocked.CompareExchange(ref _newSnapshotNeeded, 1, 0);
            OnPropertyChanged("Count");
        }

        private bool IsDeferred => _deferCount > 0;

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
            if (--_deferCount == 0) {
                ProcessDefer();
            }
        }

        private void ProcessDefer() {
            foreach (var key in _deferredPropertyChanges)
                OnPropertyChanged(key);
            _deferredPropertyChanges.Clear();
            foreach (var args in _deferredCollectionChanges)
                OnCollectionChanged(args);
            _deferredCollectionChanges.Clear();
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

        private ReadOnlyCollection<TValue> UpdateSnapshot() {
            if (_newSnapshotNeeded <= 0) return _snapshot;
            lock (_snapshotLock)
                if (Interlocked.CompareExchange(ref _newSnapshotNeeded, 0, 1) == 1)
                    _snapshot = new ReadOnlyCollection<TValue>(_store.Values.ToList());
            return _snapshot;
        }

        /// <summary>
        /// Gets an immutable snapshot of the collection
        /// </summary>
        public ReadOnlyCollection<TValue> Snapshot {
            get {
                return DoRead(
                    () => { return UpdateSnapshot(); });
            }
        }

        public object SyncRoot => this;

        public bool IsSynchronized => true;

        public int Count {
            get { return DoRead(() => _store.Count); }
        }

        public bool IsReadOnly => false;

        public ICollection<TKey> Keys {
            get { return DoRead(() => _store.Keys); }
        }

        public ICollection<TValue> Values {
            get { return DoRead(() => _store.Values); }
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

        public IEnumerator<TValue> GetEnumerator() {
            return Snapshot.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return Snapshot.GetEnumerator();
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

            value = default(TValue);
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

        public void CopyTo(Array array, int index) {
            DoRead(() => _store.ToArray().CopyTo(array, index));
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
            TValue innerValue = default(TValue);
            var result = DoRead(() => _store.TryGetValue(key, out innerValue));
            value = innerValue;
            return result;
        }

        public TValue this[TKey key] {
            get { return DoRead(() => _store[key]); }
            set {
                DoWrite(() => {
                    BinaryTreeNode<KeyValuePair<TKey, TValue>> node;
                    var index = _store.IndexOfKey(key, out node);
                    if (node == null) {
                        BaseAdd(new KeyValuePair<TKey, TValue>(key, value));
                    }
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

        private void OnCollectionChangedForKey(TKey key, NotifyCollectionChangedEventArgs args) {
            OnCollectionChanged(args);
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        internal void OnCollectionChanged(NotifyCollectionChangedEventArgs args) {
            if (IsDeferred) {
                _deferredCollectionChanges.Add(args);
                return;
            }

            var handler = CollectionChanged;
            if (handler != null)
                handler(this, args);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name) {
            if (IsDeferred) {
                _deferredPropertyChanges.Add(name);
                return;
            }

            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }

    public class AVLTreeNode<T> : BinaryTreeNode<T> {
        public AVLTreeNode(T value)
            : base(value) { }

        public new AVLTreeNode<T> LeftChild {
            get => (AVLTreeNode<T>) base.LeftChild;
            set => base.LeftChild = value;
        }

        public new AVLTreeNode<T> RightChild {
            get => (AVLTreeNode<T>) base.RightChild;
            set => base.RightChild = value;
        }

        public new AVLTreeNode<T> Parent {
            get => (AVLTreeNode<T>) base.Parent;
            set => base.Parent = value;
        }

        public int Balance { get; set; }
    }

    /// <summary>
    /// AVL Tree data structure
    /// </summary>
    public class AVLTree<TKey, TValue> : BinaryTree<TKey, TValue> {
        public AVLTree() { }

        public AVLTree(IComparer<TKey> comparer)
            : base(comparer) { }

        /// <summary>
        /// Returns the AVL Node of the tree
        /// </summary>
        public new AVLTreeNode<KeyValuePair<TKey, TValue>> Root {
            get => (AVLTreeNode<KeyValuePair<TKey, TValue>>) base.Root;
            set => base.Root = value;
        }

        /// <summary>
        /// Returns the AVL Node corresponding to the given value
        /// </summary>
        public new AVLTreeNode<KeyValuePair<TKey, TValue>> Find(TKey key) {
            return (AVLTreeNode<KeyValuePair<TKey, TValue>>) base.Find(key);
        }

        /// <summary>
        /// Insert a value in the tree and rebalance the tree if necessary.
        /// </summary>
        public override void Add(KeyValuePair<TKey, TValue> value) {
            var node = new AVLTreeNode<KeyValuePair<TKey, TValue>>(value) {
                Size = 1
            };
            Add(node);
        }

        protected void Add(AVLTreeNode<KeyValuePair<TKey, TValue>> node) {
            base.Add(node);

            var parentNode = node.Parent;
            var offset = node.IsLeftChild ? -1 : 1;
            while (parentNode != null && offset != 0) {
                parentNode.Balance += offset;
                if (parentNode.Balance == 0)
                    break;
                offset = parentNode.IsLeftChild ? -1 : 1;
                var newParentNode = parentNode.Parent;

                if (BalanceAt(parentNode))
                    break;

                parentNode = newParentNode;
            }
        }

        /// <summary>
        /// Wrapper method for removing a node within the tree
        /// </summary>
        public override bool Remove(BinaryTreeNode<KeyValuePair<TKey, TValue>> removeNode) {
            if (removeNode == null)
                return false; //value doesn't exist or not of this tree

            //Save reference to the parent node to be removed
            var parentNode = ((AVLTreeNode<KeyValuePair<TKey, TValue>>) removeNode).Parent;
            var offset = removeNode.IsLeftChild ? -1 : 1;
            var ignoreBalancing = removeNode.LeftChild != null && removeNode.RightChild != null;
            var removed = base.Remove(removeNode);

            if (!removed)
                return false; //removing failed, no need to rebalance
            else {
                if (!ignoreBalancing) {
                    //Balance going up the tree
                    while (parentNode != null && offset != 0) {
                        parentNode.Balance -= offset;
                        offset = parentNode.IsLeftChild ? -1 : 1;
                        var newParentNode = parentNode.Parent;

                        if (parentNode.Balance != 0 && !BalanceAt(parentNode))
                            break;

                        parentNode = newParentNode;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Balances an AVL Tree node
        /// </summary>
        protected virtual bool BalanceAt(AVLTreeNode<KeyValuePair<TKey, TValue>> node) {
            if (node.Balance > 1) //right outweighs
            {
                if (node.RightChild.Balance == -1) {
                    // Right Left case

                    //Right rotation needed
                    RotateRight(node.RightChild);
                    //Left rotation needed
                    RotateLeft(node);
                    return true;
                }
                else {
                    // Right Right case
                    //Left rotation needed
                    return RotateLeft(node);
                }
            }
            else if (node.Balance < -1) //left outweighs
            {
                if (node.LeftChild.Balance == 1) {
                    // Left Right case

                    //Left rotation needed
                    RotateLeft(node.LeftChild);
                    //Right rotation needed
                    RotateRight(node);
                    return true;
                }
                else {
                    // Left Left case

                    //Right rotation needed
                    return RotateRight(node);
                }
            }

            return false;
        }

        /// <summary>
        /// Rotates a node to the left within an AVL Tree
        /// </summary>
        protected virtual bool RotateLeft(AVLTreeNode<KeyValuePair<TKey, TValue>> root) {
            if (root == null)
                return true;

            var pivot = root.RightChild;

            if (pivot == null)
                return true;
            else {
                bool heightChange = !(
                    //root.LeftChild != null &&
                    root.RightChild != null &&
                    root.RightChild.Balance == 0
                );

                var rootParent = root.Parent;          //original parent of root node
                bool makeTreeRoot = Root == root; //whether the root was the root of the entire tree

                //Rotate
                root.RightChild = pivot.LeftChild;
                if (root.RightChild != null) root.RightChild.Parent = root;
                pivot.LeftChild = root;

                //Update parents
                root.Parent = pivot;
                pivot.Parent = rootParent;

                // Update Balance
                root.Balance -= (1 + Math.Max(pivot.Balance, 0));
                pivot.Balance -= (1 - Math.Min(root.Balance, 0));

                //Update the entire tree's Root if necessary
                if (makeTreeRoot)
                    Root = pivot;

                //Update the original parent's child node
                if (rootParent != null) {
                    if (rootParent.LeftChild == root)
                        rootParent.LeftChild = pivot;
                    else
                        rootParent.RightChild = pivot;
                }

                return heightChange;
            }
        }

        /// <summary>
        /// Rotates a node to the right within an AVL Tree
        /// </summary>
        protected virtual bool RotateRight(AVLTreeNode<KeyValuePair<TKey, TValue>> root) {
            if (root == null)
                return true;

            var pivot = root.LeftChild;

            if (pivot == null)
                return true;
            else {
                bool heightChange = !(
                    root.LeftChild != null &&
                    //root.RightChild != null &&
                    root.LeftChild.Balance == 0
                );
                var rootParent = root.Parent;          //original parent of root node
                bool makeTreeRoot = Root == root; //whether the root was the root of the entire tree

                //Rotate
                root.LeftChild = pivot.RightChild;
                if (root.LeftChild != null) root.LeftChild.Parent = root;
                pivot.RightChild = root;

                //Update parents
                root.Parent = pivot;
                pivot.Parent = rootParent;

                // Update Balance
                root.Balance += (1 - Math.Min(pivot.Balance, 0));
                pivot.Balance += (1 + Math.Max(root.Balance, 0));

                //Update the entire tree's Root if necessary
                if (makeTreeRoot)
                    Root = pivot;

                //Update the original parent's child node
                if (rootParent != null) {
                    if (rootParent.LeftChild == root)
                        rootParent.LeftChild = pivot;
                    else
                        rootParent.RightChild = pivot;
                }

                return heightChange;
            }
        }
    }

    /// <summary>
    /// A Binary Tree node that holds an element and references to other tree nodes
    /// </summary>
    public class BinaryTreeNode<T> {
        /// <summary>
        /// The value stored at the node
        /// </summary>
        public T Value { get; set; }

        private BinaryTreeNode<T> _leftChild;

        /// <summary>
        /// Gets or sets the left child node
        /// </summary>
        public virtual BinaryTreeNode<T> LeftChild {
            get => _leftChild;
            set {
                _leftChild = value;
                ResetSize();
            }
        }

        private BinaryTreeNode<T> _rightChild;

        /// <summary>
        /// Gets or sets the right child node
        /// </summary>
        public virtual BinaryTreeNode<T> RightChild {
            get => _rightChild;
            set {
                _rightChild = value;
                ResetSize();
            }
        }

        /// <summary>
        /// Gets or sets the parent node
        /// </summary>
        public virtual BinaryTreeNode<T> Parent { get; set; }

        public virtual int Size { get; set; }

        protected virtual void ResetSize() {
            Size =
                (LeftChild == null ? 0 : LeftChild.Size) +
                (RightChild == null ? 0 : RightChild.Size) +
                1;
        }

        /// <summary>
        /// Gets whether the node is a leaf (has no children)
        /// </summary>
        public virtual bool IsLeaf => ChildCount == 0;

        /// <summary>
        /// Gets whether the node is the left child of its parent
        /// </summary>
        public virtual bool IsLeftChild => Parent != null && Parent.LeftChild == this;

        /// <summary>
        /// Gets whether the node is the right child of its parent
        /// </summary>
        public virtual bool IsRightChild => Parent != null && Parent.RightChild == this;

        /// <summary>
        /// Gets the number of children this node has
        /// </summary>
        public virtual int ChildCount {
            get {
                int count = 0;

                if (LeftChild != null)
                    count++;

                if (RightChild != null)
                    count++;

                return count;
            }
        }

        /// <summary>
        /// Create a new instance of a Binary Tree node
        /// </summary>
        public BinaryTreeNode(T value) {
            Value = value;
        }
    }

    /// <summary>
    /// Specifies the mode of scanning through the tree
    /// </summary>
    public enum TraversalMode {
        InOrder = 0,
        PostOrder,
        PreOrder
    }

    /// <summary>
    /// Binary Tree data structure
    /// </summary>
    public class BinaryTree<TKey, TValue> : IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>> {
        private BinaryTreeNode<KeyValuePair<TKey, TValue>> head;
        protected readonly IComparer<TKey> comparer;
        private TraversalMode traversalMode = TraversalMode.InOrder;

        /// <summary>
        /// Gets or sets the root of the tree (the top-most node)
        /// </summary>
        public virtual BinaryTreeNode<KeyValuePair<TKey, TValue>> Root {
            get => head;
            set => head = value;
        }

        /// <summary>
        /// Gets whether the tree is read-only
        /// </summary>
        public virtual bool IsReadOnly => false;

        /// <summary>
        /// Gets the number of elements stored in the tree
        /// </summary>
        public virtual int Count => head == null ? 0 : head.Size;

        /// <summary>
        /// Gets or sets the traversal mode of the tree
        /// </summary>
        public virtual TraversalMode TraversalOrder {
            get => traversalMode;
            set => traversalMode = value;
        }

        /// <summary>
        /// Creates a new instance of a Binary Tree
        /// </summary>
        public BinaryTree() {
            comparer = Comparer<TKey>.Default;
        }

        public BinaryTree(IComparer<TKey> comparer) {
            this.comparer = comparer;
        }


        public void Add(TKey key, TValue value) {
            Add(new KeyValuePair<TKey, TValue>(key, value));
        }

        /// <summary>
        /// Adds a new element to the tree
        /// </summary>
        public virtual void Add(KeyValuePair<TKey, TValue> value) {
            BinaryTreeNode<KeyValuePair<TKey, TValue>> node =
                new BinaryTreeNode<KeyValuePair<TKey, TValue>>(value);
            Add(node);
        }

        /// <summary>
        /// Adds a node to the tree
        /// </summary>
        public virtual void Add(BinaryTreeNode<KeyValuePair<TKey, TValue>> node) {
            if (head == null) //first element being added
            {
                head = node; //set node as root of the tree
                node.Size = 1;
            }
            else {
                if (node.Parent == null) {
                    node.Parent = head; //start at head
                    node.Size = 1;
                }

                node.Parent.Size++;

                //Node is inserted on the left side if it is smaller or equal to the parent
                bool insertLeftSide =
                    comparer.Compare(node.Value.Key, node.Parent.Value.Key) <= 0;

                if (insertLeftSide) //insert on the left
                {
                    if (node.Parent.LeftChild == null) {
                        node.Parent.LeftChild = node; //insert in left
                    }
                    else {
                        node.Parent = node.Parent.LeftChild; //scan down to left child
                        Add(node);                      //recursive call
                    }
                }
                else //insert on the right
                {
                    if (node.Parent.RightChild == null) {
                        node.Parent.RightChild = node; //insert in right
                    }
                    else {
                        node.Parent = node.Parent.RightChild;
                        Add(node);
                    }
                }
            }
        }

        protected virtual BinaryTreeNode<KeyValuePair<TKey, TValue>> Find(TKey key) {
            var node = Root; //start at head
            while (node != null) {
                var r = comparer.Compare(key, node.Value.Key);
                if (r == 0) //parameter value found
                    return node;
                else {
                    //Search left if the value is smaller than the current node
                    if (r < 0)
                        node = node.LeftChild; //search left
                    else
                        node = node.RightChild; //search right
                }
            }

            return null;
        }

        /// <summary>
        /// Returns whether a value is stored in the tree
        /// </summary>
        public virtual bool Contains(KeyValuePair<TKey, TValue> value) {
            var node = Find(value.Key);
            if (node == null)
                return false;
            return Comparer<TValue>.Default.Compare(node.Value.Value, value.Value) == 0;
        }

        public virtual bool ContainsKey(TKey key) {
            return Find(key) != null;
        }

        /// <summary>
        /// Removes a value from the tree and returns whether the removal was successful.
        /// </summary>
        public virtual bool Remove(KeyValuePair<TKey, TValue> value) {
            var removeNode = Find(value.Key);
            return Remove(removeNode);
        }

        public virtual bool Remove(TKey key) {
            var removeNode = Find(key);
            return Remove(removeNode);
        }

        /// <summary>
        /// Removes a node from the tree and returns whether the removal was successful.
        /// </summary>>
        public virtual bool Remove(BinaryTreeNode<KeyValuePair<TKey, TValue>> removeNode) {
            if (removeNode == null)
                return false; //value doesn't exist or not of this tree

            //Note whether the node to be removed is the root of the tree
            bool wasHead = (removeNode == head);

            if (Count == 1) {
                head = null; //only element was the root
            }
            else if (removeNode.IsLeaf) //Case 1: No Children
            {
                var parent = removeNode.Parent;
                while (parent != null) {
                    parent.Size--;
                    parent = parent.Parent;
                }

                //Remove node from its parent
                if (removeNode.IsLeftChild)
                    removeNode.Parent.LeftChild = null;
                else
                    removeNode.Parent.RightChild = null;

                removeNode.Parent = null;
            }
            else if (removeNode.ChildCount == 1) //Case 2: One Child
            {
                var parent = removeNode.Parent;
                while (parent != null) {
                    parent.Size--;
                    parent = parent.Parent;
                }

                if (removeNode.LeftChild != null) {
                    //Put left child node in place of the node to be removed
                    removeNode.LeftChild.Parent = removeNode.Parent; //update parent

                    if (wasHead)
                        Root = removeNode.LeftChild; //update root reference if needed
                    else {
                        if (removeNode.IsLeftChild) //update the parent's child reference
                            removeNode.Parent.LeftChild = removeNode.LeftChild;
                        else
                            removeNode.Parent.RightChild = removeNode.LeftChild;
                    }
                }
                else //Has right child
                {
                    //Put left node in place of the node to be removed
                    removeNode.RightChild.Parent = removeNode.Parent; //update parent

                    if (wasHead)
                        Root = removeNode.RightChild; //update root reference if needed
                    else {
                        if (removeNode.IsLeftChild) //update the parent's child reference
                            removeNode.Parent.LeftChild = removeNode.RightChild;
                        else
                            removeNode.Parent.RightChild = removeNode.RightChild;
                    }
                }

                removeNode.Parent = null;
                removeNode.LeftChild = null;
                removeNode.RightChild = null;
            }
            else //Case 3: Two Children
            {
                // Find the nearest element with only 1 or less children.
                // From the right subtree, find the left most children
                var successorNode = removeNode.RightChild;
                while (successorNode.LeftChild != null)
                    successorNode = successorNode.LeftChild;

                removeNode.Value = successorNode.Value; // Swap the value

                Remove(successorNode); //recursively remove the inorder predecessor
            }


            return true;
        }

        /// <summary>
        /// Removes all the elements stored in the tree
        /// </summary>
        public virtual void Clear() {
            Root = null;
        }

        /// <summary>
        /// Returns the depth of a subtree rooted at the parameter value
        /// </summary>
        public virtual int GetDepth(KeyValuePair<TKey, TValue> value) {
            var node = Find(value.Key);
            return GetDepth(node);
        }

        /// <summary>
        /// Returns the depth of a subtree rooted at the parameter node
        /// </summary>
        public virtual int GetDepth(BinaryTreeNode<KeyValuePair<TKey, TValue>> startNode) {
            int depth = 0;

            if (startNode == null)
                return depth;

            var parentNode = startNode.Parent; //start a node above
            while (parentNode != null) {
                depth++;
                parentNode = parentNode.Parent; //scan up towards the root
            }

            return depth;
        }


        public ICollection<TKey> Keys => (from i in this select i.Key).ToArray();


        public ICollection<TValue> Values => (from i in this select i.Value).ToArray();

        public bool TryGetValue(TKey key, out TValue value) {
            var node = Find(key);
            if (node == null) {
                value = default(TValue);
                return false;
            }

            value = node.Value.Value;
            return true;
        }

        public TValue this[TKey key] {
            get {
                var node = Find(key);
                if (node == null) throw new KeyNotFoundException();
                return node.Value.Value;
            }
            set {
                var node = Find(key);
                if (node == null) throw new KeyNotFoundException();
                node.Value = new KeyValuePair<TKey, TValue>(node.Value.Key, value);
            }
        }

        public int IndexOfKey(TKey key) {
            BinaryTreeNode<KeyValuePair<TKey, TValue>> node;
            return IndexOfKey(key, out node);
        }

        public int IndexOfKey(TKey key, out BinaryTreeNode<KeyValuePair<TKey, TValue>> node) {
            int size = 0;
            node = Root; //start at head
            while (node != null) {
                var r = comparer.Compare(key, node.Value.Key);
                if (r == 0) //parameter value found
                    return size + (node.LeftChild != null ? node.LeftChild.Size : 0);
                else {
                    //Search left if the value is smaller than the current node
                    if (r < 0) {
                        node = node.LeftChild; //search left
                    }
                    else {
                        size++;
                        if (node.LeftChild != null)
                            size += node.LeftChild.Size;
                        node = node.RightChild; //search right
                    }
                }
            }

            node = null;
            return -1;
        }

        /// <summary>
        /// Returns an enumerator to scan through the elements stored in tree.
        /// The enumerator uses the traversal set in the TraversalMode property.
        /// </summary>
        public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            switch (TraversalOrder) {
                case TraversalMode.InOrder:
                    return GetInOrderEnumerator();
                case TraversalMode.PostOrder:
                    return GetPostOrderEnumerator();
                case TraversalMode.PreOrder:
                    return GetPreOrderEnumerator();
                default:
                    return GetInOrderEnumerator();
            }
        }

        /// <summary>
        /// Returns an enumerator to scan through the elements stored in tree.
        /// The enumerator uses the traversal set in the TraversalMode property.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that visits node in the order: left child, parent, right child
        /// </summary>
        public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetInOrderEnumerator() {
            return new BinaryTreeInOrderEnumerator(this);
        }

        /// <summary>
        /// Returns an enumerator that visits node in the order: left child, right child, parent
        /// </summary>
        public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetPostOrderEnumerator() {
            return new BinaryTreePostOrderEnumerator(this);
        }

        /// <summary>
        /// Returns an enumerator that visits node in the order: parent, left child, right child
        /// </summary>
        public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetPreOrderEnumerator() {
            return new BinaryTreePreOrderEnumerator(this);
        }

        /// <summary>
        /// Copies the elements in the tree to an array using the traversal mode specified.
        /// </summary>
        public virtual void CopyTo(KeyValuePair<TKey, TValue>[] array) {
            CopyTo(array, 0);
        }

        /// <summary>
        /// Copies the elements in the tree to an array using the traversal mode specified.
        /// </summary>
        public virtual void CopyTo(KeyValuePair<TKey, TValue>[] array, int startIndex) {
            using var enumerator = GetEnumerator();

            for (int i = startIndex; i < array.Length; i++) {
                if (enumerator.MoveNext())
                    array[i] = enumerator.Current;
                else
                    break;
            }
        }

        /// <summary>
        /// Compares two elements to determine their positions within the tree.
        /// </summary>
        public static int CompareElements(IComparable x, IComparable y) {
            return x.CompareTo(y);
        }

        internal abstract class BinaryTreeEnumeratorBase : IEnumerator<KeyValuePair<TKey, TValue>> {
            private BinaryTreeNode<KeyValuePair<TKey, TValue>> current;
            private BinaryTree<TKey, TValue> tree;
            protected Queue<BinaryTreeNode<KeyValuePair<TKey, TValue>>> traverseQueue;

            public BinaryTreeEnumeratorBase(BinaryTree<TKey, TValue> tree) {
                this.tree = tree;

                //Build queue
                traverseQueue = new Queue<BinaryTreeNode<KeyValuePair<TKey, TValue>>>();
                visitNode(this.tree.Root);
            }

            protected abstract void visitNode(BinaryTreeNode<KeyValuePair<TKey, TValue>> node);

            public KeyValuePair<TKey, TValue> Current => current.Value;

            object IEnumerator.Current => Current;

            public void Dispose() {
                current = null;
                tree = null;
            }

            public void Reset() {
                current = null;
            }

            public bool MoveNext() {
                if (traverseQueue.Count > 0)
                    current = traverseQueue.Dequeue();
                else
                    current = null;

                return (current != null);
            }
        }


        /// <summary>
        /// Returns an inorder-traversal enumerator for the tree values
        /// </summary>
        internal class BinaryTreeInOrderEnumerator : BinaryTreeEnumeratorBase {
            public BinaryTreeInOrderEnumerator(BinaryTree<TKey, TValue> tree)
                : base(tree) { }

            protected override void visitNode(BinaryTreeNode<KeyValuePair<TKey, TValue>> node) {
                if (node == null)
                    return;
                visitNode(node.LeftChild);
                traverseQueue.Enqueue(node);
                visitNode(node.RightChild);
            }
        }

        /// <summary>
        /// Returns a postorder-traversal enumerator for the tree values
        /// </summary>
        internal class BinaryTreePostOrderEnumerator : BinaryTreeEnumeratorBase {
            public BinaryTreePostOrderEnumerator(BinaryTree<TKey, TValue> tree)
                : base(tree) { }

            protected override void visitNode(BinaryTreeNode<KeyValuePair<TKey, TValue>> node) {
                if (node == null)
                    return;
                visitNode(node.LeftChild);
                visitNode(node.RightChild);
                traverseQueue.Enqueue(node);
            }
        }

        /// <summary>
        /// Returns an preorder-traversal enumerator for the tree values
        /// </summary>
        internal class BinaryTreePreOrderEnumerator : BinaryTreeEnumeratorBase {
            public BinaryTreePreOrderEnumerator(BinaryTree<TKey, TValue> tree)
                : base(tree) { }

            protected override void visitNode(BinaryTreeNode<KeyValuePair<TKey, TValue>> node) {
                if (node == null)
                    return;
                traverseQueue.Enqueue(node);
                visitNode(node.LeftChild);
                visitNode(node.RightChild);
            }
        }
    }
}