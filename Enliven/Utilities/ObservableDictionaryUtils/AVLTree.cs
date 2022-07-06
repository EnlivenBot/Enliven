using System;
using System.Collections.Generic;

namespace Bot.Utilities.ObservableDictionaryUtils {
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

                var rootParent = root.Parent;     //original parent of root node
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
                var rootParent = root.Parent;     //original parent of root node
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
}