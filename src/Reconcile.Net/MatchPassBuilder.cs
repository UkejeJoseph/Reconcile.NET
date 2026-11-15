using Reconcile.Net.Internal;

namespace Reconcile.Net;

/// <summary>Declares the criteria of one match pass. All declared criteria must hold for a pair to match.</summary>
/// <typeparam name="TLeft">The record type of the left stream.</typeparam>
/// <typeparam name="TRight">The record type of the right stream.</typeparam>
public sealed class MatchPassBuilder<TLeft, TRight>
{
    private const decimal ExactAmountTolerance = 0m;

    private readonly PairPassDefinition<TLeft, TRight> _pass;

    internal MatchPassBuilder(PairPassDefinition<TLeft, TRight> pass) => _pass = pass;

    /// <summary>
    /// Adds an exact key criterion. Calling it more than once builds a composite key:
    /// records match only when every key part is equal.
    /// Null never equals null: a record whose key selector returns null (any part of a composite key)
    /// never key-matches on this pass. It stays available to later passes and otherwise surfaces in
    /// the unmatched lists, where a blank-reference row belongs.
    /// </summary>
    /// <typeparam name="TKey">The key type. Compared with its default equality.</typeparam>
    /// <param name="leftKey">Selects the key from a left record.</param>
    /// <param name="rightKey">Selects the key from a right record.</param>
    /// <returns>The same pass builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When either selector is null.</exception>
    public MatchPassBuilder<TLeft, TRight> Key<TKey>(
        Func<TLeft, TKey> leftKey,
        Func<TRight, TKey> rightKey)
    {
        ArgumentNullException.ThrowIfNull(leftKey);
        ArgumentNullException.ThrowIfNull(rightKey);
        _pass.KeyParts.Add(KeyPart<TLeft, TRight>.Create(leftKey, rightKey));
        return this;
    }

    /// <summary>
    /// Adds an amount criterion with an absolute tolerance. The boundary is inclusive:
    /// amounts whose difference equals the tolerance still match. The default tolerance is zero (exact).
    /// In a pass with no Key criterion every remaining left record is compared against every remaining
    /// candidate, O(n*m) in the worst case; keep at least one Key in passes that run over large remainders.
    /// </summary>
    /// <param name="leftAmount">Selects the amount from a left record.</param>
    /// <param name="rightAmount">Selects the amount from a right record.</param>
    /// <param name="tolerance">The largest allowed absolute difference between the two amounts.</param>
    /// <returns>The same pass builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When either selector is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="tolerance"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">When an amount criterion is already defined for this pass.</exception>
    public MatchPassBuilder<TLeft, TRight> Amount(
        Func<TLeft, decimal> leftAmount,
        Func<TRight, decimal> rightAmount,
        decimal tolerance = ExactAmountTolerance)
    {
        ArgumentNullException.ThrowIfNull(leftAmount);
        ArgumentNullException.ThrowIfNull(rightAmount);
        if (tolerance < ExactAmountTolerance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tolerance), tolerance, "Amount tolerance cannot be negative.");
        }

        SetAmount(AmountCriterion<TLeft, TRight>.Absolute(leftAmount, rightAmount, tolerance));
        return this;
    }

    /// <summary>
    /// Adds an amount criterion with a percentage tolerance, relative to the right amount.
    /// The boundary is inclusive. A right amount of zero therefore requires an exact match.
    /// In a pass with no Key criterion every remaining left record is compared against every remaining
    /// candidate, O(n*m) in the worst case; keep at least one Key in passes that run over large remainders.
    /// </summary>
    /// <param name="leftAmount">Selects the amount from a left record.</param>
    /// <param name="rightAmount">Selects the amount from a right record.</param>
    /// <param name="tolerancePercent">The largest allowed difference, as a percentage of the absolute right amount.</param>
    /// <returns>The same pass builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When either selector is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="tolerancePercent"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">When an amount criterion is already defined for this pass.</exception>
    public MatchPassBuilder<TLeft, TRight> AmountPercent(
        Func<TLeft, decimal> leftAmount,
        Func<TRight, decimal> rightAmount,
        decimal tolerancePercent)
    {
        ArgumentNullException.ThrowIfNull(leftAmount);
        ArgumentNullException.ThrowIfNull(rightAmount);
        if (tolerancePercent < ExactAmountTolerance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tolerancePercent), tolerancePercent, "Amount tolerance cannot be negative.");
        }

        SetAmount(AmountCriterion<TLeft, TRight>.Percent(leftAmount, rightAmount, tolerancePercent));
        return this;
    }

    /// <summary>
    /// Adds a date criterion where either side may be up to <paramref name="window"/> later than the other.
    /// Dates are compared by raw tick arithmetic; <see cref="DateTimeKind"/> is ignored, so normalize both
    /// sides to one timezone in your selectors. In a pass with no Key criterion every remaining left record
    /// is compared against every remaining candidate, O(n*m) in the worst case.
    /// </summary>
    /// <param name="leftDate">Selects the date from a left record.</param>
    /// <param name="rightDate">Selects the date from a right record.</param>
    /// <param name="window">The largest allowed difference between the two dates. Inclusive.</param>
    /// <returns>The same pass builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When either selector is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="window"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">When a date criterion is already defined for this pass.</exception>
    public MatchPassBuilder<TLeft, TRight> Date(
        Func<TLeft, DateTime> leftDate,
        Func<TRight, DateTime> rightDate,
        TimeSpan window)
        => Date(leftDate, rightDate, window, DateWindowDirection.Either);

    /// <summary>
    /// Adds a date criterion with explicit control over which side may lag.
    /// Dates are compared by raw tick arithmetic; <see cref="DateTimeKind"/> is ignored, so normalize both
    /// sides to one timezone in your selectors. In a pass with no Key criterion every remaining left record
    /// is compared against every remaining candidate, O(n*m) in the worst case.
    /// </summary>
    /// <param name="leftDate">Selects the date from a left record.</param>
    /// <param name="rightDate">Selects the date from a right record.</param>
    /// <param name="window">The largest allowed difference between the two dates. Inclusive.</param>
    /// <param name="direction">Which side is allowed to be later than the other.</param>
    /// <returns>The same pass builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When either selector is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="window"/> is negative or <paramref name="direction"/> is not a defined value.</exception>
    /// <exception cref="InvalidOperationException">When a date criterion is already defined for this pass.</exception>
    public MatchPassBuilder<TLeft, TRight> Date(
        Func<TLeft, DateTime> leftDate,
        Func<TRight, DateTime> rightDate,
        TimeSpan window,
        DateWindowDirection direction)
    {
        ArgumentNullException.ThrowIfNull(leftDate);
        ArgumentNullException.ThrowIfNull(rightDate);
        if (window < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(window), window, "Date window cannot be negative.");
        }

        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction), direction, $"Unknown {nameof(DateWindowDirection)} value.");
        }

        if (_pass.Date is not null)
        {
            throw new InvalidOperationException("A date criterion is already defined for this pass.");
        }

        _pass.Date = new DateCriterion<TLeft, TRight>(leftDate, rightDate, window, direction);
        return this;
    }

    private void SetAmount(AmountCriterion<TLeft, TRight> criterion)
    {
        if (_pass.Amount is not null)
        {
            throw new InvalidOperationException("An amount criterion is already defined for this pass.");
        }

        _pass.Amount = criterion;
    }
}
