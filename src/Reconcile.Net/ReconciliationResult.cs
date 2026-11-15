using Reconcile.Net.Internal;

namespace Reconcile.Net;

/// <summary>The immutable outcome of one reconciliation run.</summary>
/// <typeparam name="TLeft">The record type of the left stream.</typeparam>
/// <typeparam name="TRight">The record type of the right stream.</typeparam>
public sealed class ReconciliationResult<TLeft, TRight>
{
    internal ReconciliationResult(
        IReadOnlyList<MatchedPair<TLeft, TRight>> matched,
        IReadOnlyList<MatchedGroup<TLeft, TRight>> matchedGroups,
        IReadOnlyList<TLeft> unmatchedLeft,
        IReadOnlyList<TRight> unmatchedRight,
        ReconciliationSummary summary)
    {
        Matched = matched;
        MatchedGroups = matchedGroups;
        UnmatchedLeft = unmatchedLeft;
        UnmatchedRight = unmatchedRight;
        Summary = summary;
    }

    /// <summary>One-to-one matches, ordered by pass and then by original left order.</summary>
    public IReadOnlyList<MatchedPair<TLeft, TRight>> Matched { get; }

    /// <summary>One-to-many group matches, ordered by pass and then by original right order.</summary>
    public IReadOnlyList<MatchedGroup<TLeft, TRight>> MatchedGroups { get; }

    /// <summary>Left records no pass matched, in their original stream order.</summary>
    public IReadOnlyList<TLeft> UnmatchedLeft { get; }

    /// <summary>Right records no pass matched, in their original stream order.</summary>
    public IReadOnlyList<TRight> UnmatchedRight { get; }

    /// <summary>Aggregate counts and amount totals for this run.</summary>
    public ReconciliationSummary Summary { get; }

    /// <summary>
    /// Computes true amount totals across every match made by every pass, including key-only passes,
    /// using the supplied selectors. Matched and unmatched totals together always equal the input
    /// totals on each side.
    /// </summary>
    /// <param name="leftAmount">Selects the amount from a left record.</param>
    /// <param name="rightAmount">Selects the amount from a right record.</param>
    /// <returns>Totals over all matched pairs, all matched groups, and all unmatched records.</returns>
    /// <exception cref="ArgumentNullException">When either selector is null.</exception>
    public AmountTotals SumAmounts(Func<TLeft, decimal> leftAmount, Func<TRight, decimal> rightAmount)
    {
        ArgumentNullException.ThrowIfNull(leftAmount);
        ArgumentNullException.ThrowIfNull(rightAmount);

        var matchedLeftTotal = 0m;
        var matchedRightTotal = 0m;
        foreach (var pair in Matched)
        {
            matchedLeftTotal += leftAmount(pair.Left);
            matchedRightTotal += rightAmount(pair.Right);
        }

        foreach (var group in MatchedGroups)
        {
            foreach (var left in group.Lefts)
            {
                matchedLeftTotal += leftAmount(left);
            }

            matchedRightTotal += rightAmount(group.Right);
        }

        var unmatchedLeftTotal = 0m;
        foreach (var left in UnmatchedLeft)
        {
            unmatchedLeftTotal += leftAmount(left);
        }

        var unmatchedRightTotal = 0m;
        foreach (var right in UnmatchedRight)
        {
            unmatchedRightTotal += rightAmount(right);
        }

        return new AmountTotals(
            matchedLeftTotal,
            matchedRightTotal,
            matchedLeftTotal - matchedRightTotal,
            unmatchedLeftTotal,
            unmatchedRightTotal);
    }

    /// <summary>Groups the unmatched left records into the default age bands: 0-7, 8-30, 31-90, and 91+ days.</summary>
    /// <param name="dateSelector">Selects the date a record was posted or should have settled.</param>
    /// <param name="asOf">The date ages are measured against. Ages are whole calendar days; future dates count as zero. The <see cref="DateTimeKind"/> of the selected dates and of this value is ignored, so normalize both sides to one timezone before aging.</param>
    /// <returns>An aging report over <see cref="UnmatchedLeft"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="dateSelector"/> is null.</exception>
    public AgingReport<TLeft> AgeUnmatchedLeft(Func<TLeft, DateTime> dateSelector, DateTime asOf)
        => AgingCalculator.Build(
            UnmatchedLeft, dateSelector, asOf, AgingCalculator.DefaultBucketUpperBoundsInDays);

    /// <summary>Groups the unmatched left records into custom age bands.</summary>
    /// <param name="dateSelector">Selects the date a record was posted or should have settled.</param>
    /// <param name="asOf">The date ages are measured against. Ages are whole calendar days; future dates count as zero. The <see cref="DateTimeKind"/> of the selected dates and of this value is ignored, so normalize both sides to one timezone before aging.</param>
    /// <param name="bucketUpperBoundsInDays">Inclusive upper bounds of the closed bands, in days, strictly ascending and positive. A final open-ended band is always appended.</param>
    /// <returns>An aging report over <see cref="UnmatchedLeft"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="dateSelector"/> or <paramref name="bucketUpperBoundsInDays"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="bucketUpperBoundsInDays"/> is empty, not ascending, or not positive.</exception>
    public AgingReport<TLeft> AgeUnmatchedLeft(
        Func<TLeft, DateTime> dateSelector,
        DateTime asOf,
        IReadOnlyList<int> bucketUpperBoundsInDays)
        => AgingCalculator.Build(UnmatchedLeft, dateSelector, asOf, bucketUpperBoundsInDays);

    /// <summary>Groups the unmatched right records into the default age bands: 0-7, 8-30, 31-90, and 91+ days.</summary>
    /// <param name="dateSelector">Selects the date a record was posted or should have settled.</param>
    /// <param name="asOf">The date ages are measured against. Ages are whole calendar days; future dates count as zero. The <see cref="DateTimeKind"/> of the selected dates and of this value is ignored, so normalize both sides to one timezone before aging.</param>
    /// <returns>An aging report over <see cref="UnmatchedRight"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="dateSelector"/> is null.</exception>
    public AgingReport<TRight> AgeUnmatchedRight(Func<TRight, DateTime> dateSelector, DateTime asOf)
        => AgingCalculator.Build(
            UnmatchedRight, dateSelector, asOf, AgingCalculator.DefaultBucketUpperBoundsInDays);

    /// <summary>Groups the unmatched right records into custom age bands.</summary>
    /// <param name="dateSelector">Selects the date a record was posted or should have settled.</param>
    /// <param name="asOf">The date ages are measured against. Ages are whole calendar days; future dates count as zero. The <see cref="DateTimeKind"/> of the selected dates and of this value is ignored, so normalize both sides to one timezone before aging.</param>
    /// <param name="bucketUpperBoundsInDays">Inclusive upper bounds of the closed bands, in days, strictly ascending and positive. A final open-ended band is always appended.</param>
    /// <returns>An aging report over <see cref="UnmatchedRight"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="dateSelector"/> or <paramref name="bucketUpperBoundsInDays"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="bucketUpperBoundsInDays"/> is empty, not ascending, or not positive.</exception>
    public AgingReport<TRight> AgeUnmatchedRight(
        Func<TRight, DateTime> dateSelector,
        DateTime asOf,
        IReadOnlyList<int> bucketUpperBoundsInDays)
        => AgingCalculator.Build(UnmatchedRight, dateSelector, asOf, bucketUpperBoundsInDays);
}
