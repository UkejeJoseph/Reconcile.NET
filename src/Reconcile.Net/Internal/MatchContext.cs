namespace Reconcile.Net.Internal;

internal sealed class MatchContext<TLeft, TRight>
{
    public MatchContext(TLeft[] left, TRight[] right)
    {
        Left = left;
        Right = right;
        LeftMatched = new bool[left.Length];
        RightMatched = new bool[right.Length];
    }

    public TLeft[] Left { get; }

    public TRight[] Right { get; }

    public bool[] LeftMatched { get; }

    public bool[] RightMatched { get; }

    public List<MatchedPair<TLeft, TRight>> Pairs { get; } = new();

    public List<MatchedGroup<TLeft, TRight>> Groups { get; } = new();

    public decimal AmountBearingMatchedLeftTotal { get; set; }

    public decimal AmountBearingMatchedRightTotal { get; set; }

    public decimal AmountBearingVarianceTotal { get; set; }
}
