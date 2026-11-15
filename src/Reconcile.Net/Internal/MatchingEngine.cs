namespace Reconcile.Net.Internal;

internal static class MatchingEngine
{
    private const int FirstPassNumber = 1;

    public static ReconciliationResult<TLeft, TRight> Run<TLeft, TRight>(
        IEnumerable<TLeft> leftSource,
        IEnumerable<TRight> rightSource,
        IReadOnlyList<PassDefinition<TLeft, TRight>> passes,
        ReconciliationOptions options)
    {
        var context = new MatchContext<TLeft, TRight>(leftSource.ToArray(), rightSource.ToArray());

        for (var passIndex = 0; passIndex < passes.Count; passIndex++)
        {
            passes[passIndex].Execute(context, FirstPassNumber + passIndex, options);
        }

        var unmatchedLeft = CollectUnmatched(context.Left, context.LeftMatched);
        var unmatchedRight = CollectUnmatched(context.Right, context.RightMatched);
        var summary = new ReconciliationSummary(
            context.Pairs.Count,
            context.Groups.Count,
            context.Left.Length - unmatchedLeft.Count,
            context.Right.Length - unmatchedRight.Count,
            unmatchedLeft.Count,
            unmatchedRight.Count,
            context.AmountBearingMatchedLeftTotal,
            context.AmountBearingMatchedRightTotal,
            context.AmountBearingVarianceTotal);

        return new ReconciliationResult<TLeft, TRight>(
            context.Pairs, context.Groups, unmatchedLeft, unmatchedRight, summary);
    }

    private static IReadOnlyList<T> CollectUnmatched<T>(T[] items, bool[] matched)
    {
        var unmatched = new List<T>();
        for (var i = 0; i < items.Length; i++)
        {
            if (!matched[i])
            {
                unmatched.Add(items[i]);
            }
        }

        return unmatched;
    }
}
