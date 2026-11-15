namespace Reconcile.Net;

/// <summary>Unmatched records grouped into age bands relative to an as-of date.</summary>
/// <typeparam name="T">The record type being aged.</typeparam>
public sealed class AgingReport<T>
{
    internal AgingReport(IReadOnlyList<AgingBucket<T>> buckets)
    {
        Buckets = buckets;
        TotalCount = buckets.Sum(bucket => bucket.Items.Count);
    }

    /// <summary>The age bands, ordered from youngest to oldest.</summary>
    public IReadOnlyList<AgingBucket<T>> Buckets { get; }

    /// <summary>The total number of records across all bands.</summary>
    public int TotalCount { get; }
}
