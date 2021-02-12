using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#pragma warning disable 8714

namespace Bot.Utilities.ObservableDictionaryUtils {
    /// <summary>
    /// Binary Tree data structure
    /// </summary>
    public class BinaryTree<TKey, TValue> : IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>> {
        private BinaryTreeNode<KeyValuePair<TKey, TValue>> _head;
        protected readonly IComparer<TKey> Comparer;
        private TraversalMode _traversalMode = TraversalMode.InOrder;

        /// <summary>
        /// Gets or sets the root of the tree (the top-most node)
        /// </summary>
        public virtual BinaryTreeNode<KeyValuePair<TKey, TValue>> Root {
            get => _head;
            set => _head = value;
        }

        /// <summary>
        /// Gets whether the tree is read-only
        /// </summary>
        public virtual bool IsReadOnly => false;

        /// <summary>
        /// Gets the number of elements stored in the tree
        /// </summary>
        public virtual int Count => _head == null ? 0 : _head.Size;

        /// <summary>
        /// Gets or sets the traversal mode of the tree
        /// </summary>
        public virtual TraversalMode TraversalOrder {
            get => _traversalMode;
            set => _traversalMode = value;
        }

        /// <summary>
        /// Creates a new instance of a Binary Tree
        /// </summary>
        public BinaryTree() {
            Comparer = Comparer<TKey>.Default;
        }

        public BinaryTree(IComparer<TKey> comparer) {
            this.Comparer = comparer;
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
            if (_head == null) //first element being added
            {
                _head = node; //set node as root of the tree
                node.Size = 1;
            }
            else {
                if (node.Parent == null) {
                    node.Parent = _head; //start at head
                    node.Size = 1;
                }

                node.Parent.Size++;

                //Node is inserted on the left side if it is smaller or equal to the parent
                bool insertLeftSide =
                    Comparer.Compare(node.Value.Key, node.Parent.Value.Key) <= 0;

                if (insertLeftSide) //insert on the left
                {
                    if (node.Parent.LeftChild == null) {
                        node.Parent.LeftChild = node; //insert in left
                    }
                    else {
                        node.Parent = node.Parent.LeftChild; //scan down to left child
                        Add(node);                           //recursive call
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
                var r = Comparer.Compare(key, node.Value.Key);
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
            bool wasHead = (removeNode == _head);

            if (Count == 1) {
                _head = null; //only element was the root
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
                #pragma warning disable 8625
                removeNode.LeftChild = null;
                #pragma warning restore 8625
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
                var r = Comparer.Compare(key, node.Value.Key);
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
            private BinaryTreeNode<KeyValuePair<TKey, TValue>> _current;
            private BinaryTree<TKey, TValue> _tree;
            protected Queue<BinaryTreeNode<KeyValuePair<TKey, TValue>>> TraverseQueue;

            public BinaryTreeEnumeratorBase(BinaryTree<TKey, TValue> tree) {
                this._tree = tree;

                //Build queue
                TraverseQueue = new Queue<BinaryTreeNode<KeyValuePair<TKey, TValue>>>();
                VisitNode(this._tree.Root);
            }

            protected abstract void VisitNode(BinaryTreeNode<KeyValuePair<TKey, TValue>> node);

            public KeyValuePair<TKey, TValue> Current => _current.Value;

            object IEnumerator.Current => Current;

            public void Dispose() {
                _current = null;
                _tree = null;
            }

            public void Reset() {
                _current = null;
            }

            public bool MoveNext() {
                if (TraverseQueue.Count > 0)
                    _current = TraverseQueue.Dequeue();
                else
                    _current = null;

                return (_current != null);
            }
        }


        /// <summary>
        /// Returns an inorder-traversal enumerator for the tree values
        /// </summary>
        internal class BinaryTreeInOrderEnumerator : BinaryTreeEnumeratorBase {
            public BinaryTreeInOrderEnumerator(BinaryTree<TKey, TValue> tree)
                : base(tree) { }

            protected override void VisitNode(BinaryTreeNode<KeyValuePair<TKey, TValue>> node) {
                if (node == null)
                    return;
                VisitNode(node.LeftChild);
                TraverseQueue.Enqueue(node);
                VisitNode(node.RightChild);
            }
        }

        /// <summary>
        /// Returns a postorder-traversal enumerator for the tree values
        /// </summary>
        internal class BinaryTreePostOrderEnumerator : BinaryTreeEnumeratorBase {
            public BinaryTreePostOrderEnumerator(BinaryTree<TKey, TValue> tree)
                : base(tree) { }

            protected override void VisitNode(BinaryTreeNode<KeyValuePair<TKey, TValue>> node) {
                if (node == null)
                    return;
                VisitNode(node.LeftChild);
                VisitNode(node.RightChild);
                TraverseQueue.Enqueue(node);
            }
        }

        /// <summary>
        /// Returns an preorder-traversal enumerator for the tree values
        /// </summary>
        internal class BinaryTreePreOrderEnumerator : BinaryTreeEnumeratorBase {
            public BinaryTreePreOrderEnumerator(BinaryTree<TKey, TValue> tree)
                : base(tree) { }

            protected override void VisitNode(BinaryTreeNode<KeyValuePair<TKey, TValue>> node) {
                if (node == null)
                    return;
                TraverseQueue.Enqueue(node);
                VisitNode(node.LeftChild);
                VisitNode(node.RightChild);
            }
        }
    }
}