namespace Bot.Utilities.ObservableDictionaryUtils {
    /// <summary>
    /// A Binary Tree node that holds an element and references to other tree nodes
    /// </summary>
    public class BinaryTreeNode<T> {
        /// <summary>
        /// The value stored at the node
        /// </summary>
        public T Value { get; set; }

        private BinaryTreeNode<T> _leftChild = null!;

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

        private BinaryTreeNode<T> _rightChild = null!;

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
        public virtual BinaryTreeNode<T> Parent { get; set; } = null!;

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
}