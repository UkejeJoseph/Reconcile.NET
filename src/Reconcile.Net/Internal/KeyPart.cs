namespace Reconcile.Net.Internal;

internal sealed class KeyPart<TLeft, TRight>
{
    private KeyPart(Func<TLeft, object?> left, Func<TRight, object?> right)
    {
        Left = left;
        Right = right;
    }

    public Func<TLeft, object?> Left { get; }

    public Func<TRight, object?> Right { get; }

    public static KeyPart<TLeft, TRight> Create<TKey>(
        Func<TLeft, TKey> leftKey,
        Func<TRight, TKey> rightKey)
        => new(item => leftKey(item), item => rightKey(item));
}
