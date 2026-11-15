using System.Globalization;

namespace Reconcile.Net.Internal;

internal sealed class GroupPassDefinition<TLeft, TRight> : PassDefinition<TLeft, TRight>
{
    private const string DescriptionFormat = "Group({0})";

    public bool HasRequiredCriteria => KeyParts.Count > 0 && Amount is not null;

    public override string Describe()
        => string.Format(CultureInfo.InvariantCulture, DescriptionFormat, DescribeCriteria(date: null));

    public override void Execute(
        MatchContext<TLeft, TRight> context, int passNumber, ReconciliationOptions options)
    {
        _ = options;
        var description = Describe();
        var amount = Amount!;
        var groups = BuildLeftGroups(context, amount);

        for (var rightIndex = 0; rightIndex < context.Right.Length; rightIndex++)
        {
            if (context.RightMatched[rightIndex])
            {
                continue;
            }

            var rightItem = context.Right[rightIndex];
            if (!TryRightKeyOf(rightItem, out var key)
                || !groups.TryGetValue(key, out var group))
            {
                continue;
            }

            var rightAmount = amount.RightAmount(rightItem);
            if (!amount.IsWithinTolerance(group.Total, rightAmount))
            {
                continue;
            }

            groups.Remove(key);
            context.RightMatched[rightIndex] = true;
            var lefts = new TLeft[group.LeftIndices.Count];
            for (var i = 0; i < group.LeftIndices.Count; i++)
            {
                var leftIndex = group.LeftIndices[i];
                context.LeftMatched[leftIndex] = true;
                lefts[i] = context.Left[leftIndex];
            }

            context.AmountBearingMatchedLeftTotal += group.Total;
            context.AmountBearingMatchedRightTotal += rightAmount;
            context.AmountBearingVarianceTotal += group.Total - rightAmount;
            context.Groups.Add(new MatchedGroup<TLeft, TRight>(
                lefts, rightItem, passNumber, description, group.Total, rightAmount, group.Total - rightAmount));
        }
    }

    private Dictionary<CompositeKey, LeftGroup> BuildLeftGroups(
        MatchContext<TLeft, TRight> context,
        AmountCriterion<TLeft, TRight> amount)
    {
        var groups = new Dictionary<CompositeKey, LeftGroup>();
        for (var leftIndex = 0; leftIndex < context.Left.Length; leftIndex++)
        {
            if (context.LeftMatched[leftIndex])
            {
                continue;
            }

            var leftItem = context.Left[leftIndex];
            if (!TryLeftKeyOf(leftItem, out var key))
            {
                continue;
            }

            if (!groups.TryGetValue(key, out var group))
            {
                group = new LeftGroup();
                groups.Add(key, group);
            }

            group.LeftIndices.Add(leftIndex);
            group.Total += amount.LeftAmount(leftItem);
        }

        return groups;
    }

    private sealed class LeftGroup
    {
        public List<int> LeftIndices { get; } = new();

        public decimal Total { get; set; }
    }
}
