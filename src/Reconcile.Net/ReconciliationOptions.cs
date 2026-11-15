namespace Reconcile.Net;

/// <summary>
/// Optional guard rails for a reconciliation run. Every setting is off by default, so a builder
/// with no options behaves exactly as one without.
/// </summary>
public sealed class ReconciliationOptions
{
    /// <summary>
    /// The largest remainder product an unkeyed match pass is allowed to run over. An unkeyed pass
    /// (a <c>ThenMatchOn</c> pass with only an amount or a date criterion and no <c>Key</c>) has no
    /// dictionary to lean on and compares every remaining left record against every remaining right
    /// record, so its cost is the product of the two remaining counts. When this is set and that
    /// product exceeds the value, <see cref="ReconciliationBuilder{TLeft, TRight}.Run"/> throws an
    /// <see cref="InvalidOperationException"/> before the pass runs, turning a silent performance trap
    /// into a loud, catchable error. The remaining counts are measured after all earlier passes, so a
    /// keyed pass that trims the remainder first can bring an unkeyed pass back under the bound.
    /// Null, the default, means no limit and preserves the original behavior.
    /// </summary>
    public long? MaxUnkeyedPassProduct { get; set; }
}
