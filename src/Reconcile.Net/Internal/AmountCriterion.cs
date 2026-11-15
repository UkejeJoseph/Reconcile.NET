using System.Globalization;

namespace Reconcile.Net.Internal;

internal sealed class AmountCriterion<TLeft, TRight>
{
    private const decimal PercentDivisor = 100m;
    private const string ExactDescription = "Amount(exact)";
    private const string AbsoluteDescriptionFormat = "Amount(tolerance {0})";
    private const string PercentDescriptionFormat = "Amount(tolerance {0}%)";

    private readonly decimal _absoluteTolerance;
    private readonly decimal _percentTolerance;
    private readonly bool _isPercent;

    private AmountCriterion(
        Func<TLeft, decimal> leftAmount,
        Func<TRight, decimal> rightAmount,
        decimal absoluteTolerance,
        decimal percentTolerance,
        bool isPercent)
    {
        LeftAmount = leftAmount;
        RightAmount = rightAmount;
        _absoluteTolerance = absoluteTolerance;
        _percentTolerance = percentTolerance;
        _isPercent = isPercent;
    }

    public Func<TLeft, decimal> LeftAmount { get; }

    public Func<TRight, decimal> RightAmount { get; }

    public static AmountCriterion<TLeft, TRight> Absolute(
        Func<TLeft, decimal> leftAmount,
        Func<TRight, decimal> rightAmount,
        decimal tolerance)
        => new(leftAmount, rightAmount, tolerance, default, isPercent: false);

    public static AmountCriterion<TLeft, TRight> Percent(
        Func<TLeft, decimal> leftAmount,
        Func<TRight, decimal> rightAmount,
        decimal tolerancePercent)
        => new(leftAmount, rightAmount, default, tolerancePercent, isPercent: true);

    public bool IsWithinTolerance(decimal left, decimal right)
    {
        var allowed = _isPercent
            ? Math.Abs(right) * _percentTolerance / PercentDivisor
            : _absoluteTolerance;
        return Math.Abs(left - right) <= allowed;
    }

    public string Describe()
    {
        if (_isPercent)
        {
            return string.Format(CultureInfo.InvariantCulture, PercentDescriptionFormat, _percentTolerance);
        }

        return _absoluteTolerance == decimal.Zero
            ? ExactDescription
            : string.Format(CultureInfo.InvariantCulture, AbsoluteDescriptionFormat, _absoluteTolerance);
    }
}
