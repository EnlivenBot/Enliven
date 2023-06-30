namespace Bot.Utilities.ObservableDictionaryUtils;

public class AVLTreeNode<T> : BinaryTreeNode<T> {
    public AVLTreeNode(T value)
        : base(value) { }

    public new AVLTreeNode<T> LeftChild {
        get => (AVLTreeNode<T>)base.LeftChild;
        set => base.LeftChild = value;
    }

    public new AVLTreeNode<T> RightChild {
        get => (AVLTreeNode<T>)base.RightChild;
        set => base.RightChild = value;
    }

    public new AVLTreeNode<T> Parent {
        get => (AVLTreeNode<T>)base.Parent;
        set => base.Parent = value;
    }

    public int Balance { get; set; }
}