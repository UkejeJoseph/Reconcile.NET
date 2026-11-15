using Reconcile.Net.Internal;

namespace Reconcile.Net;

/// <summary>
/// Declares the criteria of one group pass: a group key plus an amount criterion comparing the
/// sum of the grouped left amounts against each right amount. Both are required.
/// </summary>
/// <typeparam name="TLeft">The record type of the left stream.</typeparam>
/// <typeparam name="TRight">The record type of the right stream.</typeparam>
public sealed class GroupPassBuilder<TLeft, TRight>
{
    private const decimal ExactAmountTolerance = 0m;

    private readonly GroupPassDefinition<TLeft, TRight> _pass;

    internal GroupPassBuilder(GroupPassDefinition<TLeft, TRight> pass) => _pass = pass;

    /// <summary>
    /// Adds the group key. Left records sharing the key form one group; a right record with an equal
    /// key is matched against the group's summed amount. Calling it more than once builds a composite key.
    /// Null never equals null: a record whose key selector returns null (any part of a composite key)
    /// never groups and never matches on this pass. It stays available to later passes and otherwise
    /// surfaces in the unmatched lists.
    /// </summary>
    /// <typeparam name="TKey">The key type. Compared with its default equality.</typeparam>
    /// <param name="leftKey">Selects the group key from a left record.</param>
    /// <param name="rightKey">Selects the group key from a right record.</param>
    /// <returns>The same pass builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When either selector is null.</exception>
    public GroupPassBuilder<TLeft, TRight> Key<TKey>(
        Func<TLeft, TKey> leftKey,
        Func<TRight, TKey> rightKey)
    {
        ArgumentNullException.ThrowIfNull(leftKey);
        ArgumentNullException.ThrowIfNull(rightKey);
        _pass.KeyParts.Add(KeyPart<TLeft, TRight>.Create(leftKey, rightKey));
        return this;
    }

    /// <summary>
    /// Adds the amount criterion with an absolute tolerance. The summed left amounts and the right
    /// amount match when their difference is at most the tolerance. The boundary is inclusive and
    /// the default tolerance is zero (exact).
    /// </summary>
    /// <param name="leftAmount">Selects the amount from a left record. Summed per group.</param>
    /// <param name="rightAmount">Selects the amount from a right record.</param>
    /// <param name="tolerance">The largest allowed absolute difference between group total and right amount.</param>
    /// <returns>The same pass builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When either selector is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="tolerance"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">When an amount criterion is already defined for this pass.</exception>
    public GroupPassBuilder<TLeft, TRight> Amount(
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
    /// Adds the amount criterion with a percentage tolerance, relative to the right amount.
    /// The boundary is inclusive. A right amount of zero therefore requires an exact match.
    /// </summary>
    /// <param name="leftAmount">Selects the amount from a left record. Summed per group.</param>
    /// <param name="rightAmount">Selects the amount from a right record.</param>
    /// <param name="tolerancePercent">The largest allowed difference, as a percentage of the absolute right amount.</param>
    /// <returns>The same pass builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">When either selector is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="tolerancePercent"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">When an amount criterion is already defined for this pass.</exception>
    public GroupPassBuilder<TLeft, TRight> AmountPercent(
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

    private void SetAmount(AmountCriterion<TLeft, TRight> criterion)
    {
        if (_pass.Amount is not null)
        {
            throw new InvalidOperationException("An amount criterion is already defined for this pass.");
        }

        _pass.Amount = criterion;
    }
}
