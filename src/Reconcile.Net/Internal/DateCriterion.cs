using System.Globalization;

namespace Reconcile.Net.Internal;

internal sealed class DateCriterion<TLeft, TRight>
{
    private const string DescriptionFormat = "Date(window {0}, {1})";

    public DateCriterion(
        Func<TLeft, DateTime> leftDate,
        Func<TRight, DateTime> rightDate,
        TimeSpan window,
        DateWindowDirection direction)
    {
        LeftDate = leftDate;
        RightDate = rightDate;
        Window = window;
        Direction = direction;
    }

    public Func<TLeft, DateTime> LeftDate { get; }

    public Func<TRight, DateTime> RightDate { get; }

    public TimeSpan Window { get; }

    public DateWindowDirection Direction { get; }

    public bool IsWithinWindow(DateTime left, DateTime right)
    {
        var leftLead = left - right;
        return Direction switch
        {
            DateWindowDirection.Either => leftLead.Duration() <= Window,
            DateWindowDirection.LeftMayBeLater => leftLead >= TimeSpan.Zero && leftLead <= Window,
            DateWindowDirection.RightMayBeLater => leftLead <= TimeSpan.Zero && leftLead.Duration() <= Window,
            _ => throw new InvalidOperationException($"Unknown {nameof(DateWindowDirection)}: {Direction}."),
        };
    }

    public string Describe()
        => string.Format(CultureInfo.InvariantCulture, DescriptionFormat, Window, Direction);
}
