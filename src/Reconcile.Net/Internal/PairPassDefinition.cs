namespace Reconcile.Net.Internal;

internal sealed class PairPassDefinition<TLeft, TRight> : PassDefinition<TLeft, TRight>
{
    private const int NoCandidate = -1;

    public DateCriterion<TLeft, TRight>? Date { get; set; }

    public bool HasCriteria => KeyParts.Count > 0 || Amount is not null || Date is not null;

    public override string Describe() => DescribeCriteria(Date);

    public override void Execute(
        MatchContext<TLeft, TRight> context, int passNumber, ReconciliationOptions options)
    {
        var description = Describe();
        if (KeyParts.Count == 0)
        {
            GuardUnkeyedPassSize(context, passNumber, options);
        }

        var candidates = BuildCandidateBuckets(context);

        for (var leftIndex = 0; leftIndex < context.Left.Length; leftIndex++)
        {
            if (context.LeftMatched[leftIndex])
            {
                continue;
            }

            var leftItem = context.Left[leftIndex];
            if (!TryLeftKeyOf(leftItem, out var leftKey)
                || !candidates.TryGetValue(leftKey, out var bucket))
            {
                continue;
            }

            var rightIndex = bucket.ClaimFirst(context, leftItem, this);
            if (rightIndex == NoCandidate)
            {
                continue;
            }

            context.LeftMatched[leftIndex] = true;
            RecordPair(context, leftItem, context.Right[rightIndex], passNumber, description);
        }
    }

    private static void GuardUnkeyedPassSize(
        MatchContext<TLeft, TRight> context, int passNumber, ReconciliationOptions options)
    {
        var limit = options.MaxUnkeyedPassProduct;
        if (limit is null)
        {
            return;
        }

        long remainingLeft = CountRemaining(context.LeftMatched);
        long remainingRight = CountRemaining(context.RightMatched);
        var product = remainingLeft * remainingRight;
        if (product <= limit.Value)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Unkeyed match pass {passNumber} would compare {remainingLeft} x {remainingRight} = " +
            $"{product} remaining record pairs, over the configured " +
            $"{nameof(ReconciliationOptions.MaxUnkeyedPassProduct)} of {limit.Value}. Add a Key criterion " +
            $"to this pass, raise the limit, or leave {nameof(ReconciliationOptions.MaxUnkeyedPassProduct)} " +
            "unset for unbounded matching.");
    }

    private static int CountRemaining(bool[] matched)
    {
        var remaining = 0;
        foreach (var isMatched in matched)
        {
            if (!isMatched)
            {
                remaining++;
            }
        }

        return remaining;
    }

    private bool Satisfies(TLeft left, TRight right)
        => (Amount is null || Amount.IsWithinTolerance(Amount.LeftAmount(left), Amount.RightAmount(right)))
            && (Date is null || Date.IsWithinWindow(Date.LeftDate(left), Date.RightDate(right)));

    private Dictionary<CompositeKey, CandidateBucket> BuildCandidateBuckets(
        MatchContext<TLeft, TRight> context)
    {
        var buckets = new Dictionary<CompositeKey, CandidateBucket>();
        for (var rightIndex = 0; rightIndex < context.Right.Length; rightIndex++)
        {
            if (context.RightMatched[rightIndex])
            {
                continue;
            }

            if (!TryRightKeyOf(context.Right[rightIndex], out var key))
            {
                continue;
            }

            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new CandidateBucket();
                buckets.Add(key, bucket);
            }

            bucket.Add(rightIndex);
        }

        return buckets;
    }

    private void RecordPair(
        MatchContext<TLeft, TRight> context,
        TLeft leftItem,
        TRight rightItem,
        int passNumber,
        string description)
    {
        decimal? amountVariance = null;
        if (Amount is not null)
        {
            var leftAmount = Amount.LeftAmount(leftItem);
            var rightAmount = Amount.RightAmount(rightItem);
            amountVariance = leftAmount - rightAmount;
            context.AmountBearingMatchedLeftTotal += leftAmount;
            context.AmountBearingMatchedRightTotal += rightAmount;
            context.AmountBearingVarianceTotal += leftAmount - rightAmount;
        }

        TimeSpan? dateVariance = Date is null
            ? null
            : Date.LeftDate(leftItem) - Date.RightDate(rightItem);

        context.Pairs.Add(new MatchedPair<TLeft, TRight>(
            leftItem, rightItem, passNumber, description, amountVariance, dateVariance));
    }

    private sealed class CandidateBucket
    {
        private readonly List<int> _rightIndices = new();
        private int _start;

        public void Add(int rightIndex) => _rightIndices.Add(rightIndex);

        public int ClaimFirst(
            MatchContext<TLeft, TRight> context,
            TLeft leftItem,
            PairPassDefinition<TLeft, TRight> pass)
        {
            for (var position = _start; position < _rightIndices.Count; position++)
            {
                var rightIndex = _rightIndices[position];
                if (context.RightMatched[rightIndex])
                {
                    if (position == _start)
                    {
                        _start++;
                    }

                    continue;
                }

                if (!pass.Satisfies(leftItem, context.Right[rightIndex]))
                {
                    continue;
                }

                context.RightMatched[rightIndex] = true;
                if (position == _start)
                {
                    _start++;
                }

                return rightIndex;
            }

            return NoCandidate;
        }
    }
}
