namespace Reconcile.Net;

/// <summary>
/// True amount totals over one reconciliation run, computed by
/// <see cref="ReconciliationResult{TLeft, TRight}.SumAmounts"/> with caller-supplied selectors.
/// Unlike the summary's amount-bearing totals, these cover every match made by every pass,
/// including key-only passes, plus everything left unmatched.
/// </summary>
/// <param name="MatchedLeftTotal">The sum of amounts of every matched left record, whether matched as a pair or inside a group.</param>
/// <param name="MatchedRightTotal">The sum of amounts of every matched right record.</param>
/// <param name="Variance"><paramref name="MatchedLeftTotal"/> minus <paramref name="MatchedRightTotal"/>.</param>
/// <param name="UnmatchedLeftTotal">The sum of amounts of every unmatched left record.</param>
/// <param name="UnmatchedRightTotal">The sum of amounts of every unmatched right record.</param>
public sealed record AmountTotals(
    decimal MatchedLeftTotal,
    decimal MatchedRightTotal,
    decimal Variance,
    decimal UnmatchedLeftTotal,
    decimal UnmatchedRightTotal);
