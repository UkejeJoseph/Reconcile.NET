namespace Reconcile.Net;

/// <summary>One left record matched to one right record by a match pass.</summary>
/// <typeparam name="TLeft">The record type of the left stream.</typeparam>
/// <typeparam name="TRight">The record type of the right stream.</typeparam>
/// <param name="Left">The matched left record.</param>
/// <param name="Right">The matched right record.</param>
/// <param name="PassNumber">The one-based declaration position of the pass that made the match.</param>
/// <param name="PassDescription">A human readable description of the criteria of the matching pass.</param>
/// <param name="AmountVariance">Left amount minus right amount, when the matching pass declared an amount criterion; otherwise null.</param>
/// <param name="DateVariance">Left date minus right date, when the matching pass declared a date criterion; otherwise null.</param>
public sealed record MatchedPair<TLeft, TRight>(
    TLeft Left,
    TRight Right,
    int PassNumber,
    string PassDescription,
    decimal? AmountVariance,
    TimeSpan? DateVariance);
