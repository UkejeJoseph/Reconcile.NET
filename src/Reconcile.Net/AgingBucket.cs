namespace Reconcile.Net;

/// <summary>One age band of an aging report.</summary>
/// <typeparam name="T">The record type being aged.</typeparam>
/// <param name="Label">The display label of the band, for example "8-30" or "90+".</param>
/// <param name="FromDays">The youngest age in days that falls in this band.</param>
/// <param name="ToDays">The oldest age in days that falls in this band, or null for the open-ended final band.</param>
/// <param name="Items">The records in this band, in their original stream order.</param>
public sealed record AgingBucket<T>(
    string Label,
    int FromDays,
    int? ToDays,
    IReadOnlyList<T> Items);
