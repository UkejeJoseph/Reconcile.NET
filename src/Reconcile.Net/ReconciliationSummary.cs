namespace Reconcile.Net;

/// <summary>Aggregate counts and amount totals for one reconciliation run.</summary>
/// <param name="MatchedPairCount">The number of one-to-one matched pairs.</param>
/// <param name="MatchedGroupCount">The number of matched groups.</param>
/// <param name="MatchedLeftCount">The number of left records matched by any pass, including group members.</param>
/// <param name="MatchedRightCount">The number of right records matched by any pass.</param>
/// <param name="UnmatchedLeftCount">The number of left records no pass matched.</param>
/// <param name="UnmatchedRightCount">The number of right records no pass matched.</param>
/// <param name="AmountBearingMatchedLeftTotal">The sum of left amounts over matches made by passes that declared an amount criterion, and only those. A key-only pass never reads an amount, so its matches cannot contribute here. For true totals across every match, use SumAmounts on the result.</param>
/// <param name="AmountBearingMatchedRightTotal">The sum of right amounts over matches made by passes that declared an amount criterion, and only those. For true totals across every match, use SumAmounts on the result.</param>
/// <param name="AmountBearingVarianceTotal">The signed net of left minus right over matches made by passes that declared an amount criterion, and only those.</param>
public sealed record ReconciliationSummary(
    int MatchedPairCount,
    int MatchedGroupCount,
    int MatchedLeftCount,
    int MatchedRightCount,
    int UnmatchedLeftCount,
    int UnmatchedRightCount,
    decimal AmountBearingMatchedLeftTotal,
    decimal AmountBearingMatchedRightTotal,
    decimal AmountBearingVarianceTotal);
