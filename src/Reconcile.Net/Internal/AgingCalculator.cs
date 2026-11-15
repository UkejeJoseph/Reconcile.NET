using System.Globalization;

namespace Reconcile.Net.Internal;

internal static class AgingCalculator
{
    private const int MinimumAgeDays = 0;
    private const int FirstBucketFromDays = 0;
    private const string ClosedBucketLabelFormat = "{0}-{1}";
    private const string OpenBucketLabelFormat = "{0}+";

    public static readonly int[] DefaultBucketUpperBoundsInDays = { 7, 30, 90 };

    public static AgingReport<T> Build<T>(
        IReadOnlyList<T> items,
        Func<T, DateTime> dateSelector,
        DateTime asOf,
        IReadOnlyList<int> bucketUpperBoundsInDays)
    {
        ArgumentNullException.ThrowIfNull(dateSelector);
        ArgumentNullException.ThrowIfNull(bucketUpperBoundsInDays);
        ValidateBounds(bucketUpperBoundsInDays, nameof(bucketUpperBoundsInDays));

        var bucketItems = new List<T>[bucketUpperBoundsInDays.Count + 1];
        for (var i = 0; i < bucketItems.Length; i++)
        {
            bucketItems[i] = new List<T>();
        }

        foreach (var item in items)
        {
            bucketItems[BucketIndexFor(AgeInDays(dateSelector(item), asOf), bucketUpperBoundsInDays)].Add(item);
        }

        var buckets = new AgingBucket<T>[bucketItems.Length];
        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = DescribeBucket(i, bucketUpperBoundsInDays, bucketItems[i]);
        }

        return new AgingReport<T>(buckets);
    }

    private static void ValidateBounds(IReadOnlyList<int> bounds, string parameterName)
    {
        if (bounds.Count == 0)
        {
            throw new ArgumentException("At least one bucket upper bound is required.", parameterName);
        }

        var previous = MinimumAgeDays;
        foreach (var bound in bounds)
        {
            if (bound <= previous)
            {
                throw new ArgumentException(
                    "Bucket upper bounds must be positive and strictly ascending.", parameterName);
            }

            previous = bound;
        }
    }

    private static int AgeInDays(DateTime value, DateTime asOf)
    {
        var age = (asOf.Date - value.Date).Days;
        return age < MinimumAgeDays ? MinimumAgeDays : age;
    }

    private static int BucketIndexFor(int ageDays, IReadOnlyList<int> bounds)
    {
        for (var i = 0; i < bounds.Count; i++)
        {
            if (ageDays <= bounds[i])
            {
                return i;
            }
        }

        return bounds.Count;
    }

    private static AgingBucket<T> DescribeBucket<T>(
        int index, IReadOnlyList<int> bounds, IReadOnlyList<T> items)
    {
        if (index == bounds.Count)
        {
            var openFromDays = bounds[^1] + 1;
            return new AgingBucket<T>(
                string.Format(CultureInfo.InvariantCulture, OpenBucketLabelFormat, openFromDays),
                openFromDays,
                ToDays: null,
                items);
        }

        var fromDays = index == 0 ? FirstBucketFromDays : bounds[index - 1] + 1;
        return new AgingBucket<T>(
            string.Format(CultureInfo.InvariantCulture, ClosedBucketLabelFormat, fromDays, bounds[index]),
            fromDays,
            bounds[index],
            items);
    }
}
