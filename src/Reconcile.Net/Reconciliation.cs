namespace Reconcile.Net;

/// <summary>Entry point for building a reconciliation between two record streams.</summary>
public static class Reconciliation
{
    /// <summary>Starts a reconciliation between a left stream and a right stream.</summary>
    /// <typeparam name="TLeft">The record type of the left stream, for example an internal ledger row.</typeparam>
    /// <typeparam name="TRight">The record type of the right stream, for example a settlement or bank statement row.</typeparam>
    /// <param name="left">The left stream. Enumerated once per <see cref="ReconciliationBuilder{TLeft, TRight}.Run"/>.</param>
    /// <param name="right">The right stream. Enumerated once per <see cref="ReconciliationBuilder{TLeft, TRight}.Run"/>.</param>
    /// <returns>A builder used to declare match passes and run the reconciliation.</returns>
    /// <exception cref="ArgumentNullException">When either stream is null.</exception>
    public static ReconciliationBuilder<TLeft, TRight> Between<TLeft, TRight>(
        IEnumerable<TLeft> left,
        IEnumerable<TRight> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return new ReconciliationBuilder<TLeft, TRight>(left, right);
    }
}
