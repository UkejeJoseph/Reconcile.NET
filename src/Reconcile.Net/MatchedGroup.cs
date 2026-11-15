namespace Reconcile.Net;

/// <summary>A set of left records matched, as one group, to a single right record by a group pass.</summary>
/// <typeparam name="TLeft">The record type of the left stream.</typeparam>
/// <typeparam name="TRight">The record type of the right stream.</typeparam>
/// <param name="Lefts">The left records in the group, in their original stream order.</param>
/// <param name="Right">The matched right record.</param>
/// <param name="PassNumber">The one-based declaration position of the pass that made the match.</param>
/// <param name="PassDescription">A human readable description of the criteria of the matching pass.</param>
/// <param name="LeftTotal">The sum of the amounts of the left records in the group.</param>
/// <param name="RightAmount">The amount of the right record.</param>
/// <param name="AmountVariance">Left total minus right amount, for example the fee withheld on a net settlement.</param>
public sealed record MatchedGroup<TLeft, TRight>(
    IReadOnlyList<TLeft> Lefts,
    TRight Right,
    int PassNumber,
    string PassDescription,
    decimal LeftTotal,
    decimal RightAmount,
    decimal AmountVariance);
