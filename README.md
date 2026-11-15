> **Maintained by [Joseph Ukeje](https://github.com/UkejeJoseph)**

# Reconcile.NET

Rule-based financial reconciliation engine for .NET. Match your internal ledger against processor settlement files, bank statements against internal books, invoices against payments. Zero external dependencies.

Every fintech, marketplace, and finance team reconciles: the processor says it paid you 19,900.00, your ledger says customers owed 20,000.00, and someone has to explain the difference before month end closes. Today that work lives in Excel VLOOKUPs and one-off scripts, and both break the same way: they match on a single column, they cannot express "same customer, same amount within a naira, value date up to two days later", they silently match the same row twice, and nobody can rerun last month's sheet and get the same answer. Reconcile.NET is that matching logic as a library: declarative rules, deterministic output, decimal-safe money.

What it gives you:

- Multi-pass matching: exact reference first, then fuzzier passes (composite key + amount tolerance + date window) over whatever is still unmatched
- One-to-many grouping for the gross-vs-net reality: one payout row covers many invoices, minus fees
- Amount tolerance as an absolute value or a percentage, date windows with direction control
- Aging of whatever is left over (0-7, 8-30, 31-90, 91+ days)
- Determinism guarantees you can put in an audit note

Reconcile.NET matches whatever record types you hand it, so any parser can feed it. If your bank sends SWIFT MT940 statements, the sibling [Mt940.Net](https://github.com/IsraelIyonsi/Mt940.NET) parses them into typed transactions you can pass straight in as the right-hand stream; the two packages share no dependency.

## Install

```
dotnet add package Reconcile.Net
```

## Quickstart

```csharp
using System;
using Reconcile.Net;

var ledger = new[]
{
    new LedgerRow("INV-1001", 5_000.00m, new DateTime(2026, 7, 1)),
    new LedgerRow("INV-1002", 12_500.50m, new DateTime(2026, 7, 2)),
    new LedgerRow("INV-1003", 8_000.00m, new DateTime(2026, 7, 3)),
};

var statement = new[]
{
    new StatementRow("INV-1001", 5_000.00m, new DateTime(2026, 7, 1)),
    new StatementRow("INV-1002", 12_500.50m, new DateTime(2026, 7, 3)),
    new StatementRow("BANK-CHG", 50.00m, new DateTime(2026, 7, 31)),
};

var result = Reconciliation
    .Between(ledger, statement)
    .MatchOn(l => l.Reference, r => r.Reference)
    .Run();

Console.WriteLine($"Matched: {result.Matched.Count}");                 // 2
Console.WriteLine($"Unmatched ledger rows: {result.UnmatchedLeft.Count}");    // 1
Console.WriteLine($"Unmatched statement rows: {result.UnmatchedRight.Count}"); // 1

var totals = result.SumAmounts(l => l.Amount, r => r.Amount);
Console.WriteLine($"Unreconciled ledger value: {totals.UnmatchedLeftTotal}");  // 8000.00

public sealed record LedgerRow(string Reference, decimal Amount, DateTime PostedAt);

public sealed record StatementRow(string Reference, decimal Amount, DateTime ValueDate);
```

The engine is generic over your own types; there is nothing to inherit and no mapping step. `MatchOn` may be called several times to build a composite key.

## Multi-pass matching

Real statements mangle references. Later passes run only on the records every earlier pass left unmatched, so you can start strict and relax deliberately:

```csharp
using System;
using Reconcile.Net;

var ledger = new[]
{
    new LedgerRow("INV-1003", "C3", 8_000.00m, new DateTime(2026, 7, 3)),
    new LedgerRow("INV-1004", "C4", 20_000.00m, new DateTime(2026, 7, 5)),
};

var statement = new[]
{
    new StatementRow("TRF/00234", "C3", 8_000.00m, new DateTime(2026, 7, 4)),
    new StatementRow("TRF/00235", "C4", 19_999.50m, new DateTime(2026, 7, 6)),
};

var result = Reconciliation
    .Between(ledger, statement)
    .MatchOn(l => l.Reference, r => r.Reference)
    .ThenMatchOn(pass => pass
        .Key(l => l.CustomerId, r => r.AccountRef)
        .Amount(l => l.Amount, r => r.Amount, tolerance: 1.00m)
        .Date(l => l.PostedAt, r => r.ValueDate,
            window: TimeSpan.FromDays(2),
            direction: DateWindowDirection.RightMayBeLater))
    .Run();

foreach (var pair in result.Matched)
{
    Console.WriteLine(
        $"{pair.Left.Reference} -> {pair.Right.Reference} " +
        $"(pass {pair.PassNumber}, variance {pair.AmountVariance})");
}
// INV-1003 -> TRF/00234 (pass 2, variance 0.00)
// INV-1004 -> TRF/00235 (pass 2, variance 0.50)

public sealed record LedgerRow(string Reference, string CustomerId, decimal Amount, DateTime PostedAt);

public sealed record StatementRow(string Reference, string AccountRef, decimal Amount, DateTime ValueDate);
```

Criteria available inside a pass:

| Criterion | Meaning |
|---|---|
| `Key(left, right)` | Exact equality. Repeat for a composite key. |
| `Amount(left, right)` | Exact amount equality. |
| `Amount(left, right, tolerance)` | Absolute tolerance. Inclusive: a difference exactly equal to the tolerance matches. |
| `AmountPercent(left, right, tolerancePercent)` | Tolerance as a percentage of the right amount. Inclusive. |
| `Date(left, right, window)` | Either side may be later, up to the window. Inclusive. |
| `Date(left, right, window, direction)` | `LeftMayBeLater` or `RightMayBeLater` restricts which side may lag. |

Every declared criterion must hold for a pair to match. All matched pairs carry the pass number, a description of the pass, and the signed amount and date variances (left minus right).

Two things to know about the raw values: keys returning null never match anything, not even another null (see the determinism promises), and dates are compared by raw tick arithmetic with `DateTime.Kind` ignored, so normalize both sides to one timezone in your selectors.

## One-to-many: payouts net of fees

A settlement file rarely pays invoice by invoice. One payout covers many ledger rows, minus processing fees. `ThenMatchGroups` matches one right record against the sum of all remaining left records that share its group key:

```csharp
using System;
using Reconcile.Net;

var invoices = new[]
{
    new Invoice("INV-2001", "PAYOUT-114", 30_000.00m),
    new Invoice("INV-2002", "PAYOUT-114", 45_000.00m),
    new Invoice("INV-2003", "PAYOUT-114", 25_000.00m),
};

var payouts = new[]
{
    new Payout("PAYOUT-114", 98_500.00m, new DateTime(2026, 7, 9)),
};

var result = Reconciliation
    .Between(invoices, payouts)
    .ThenMatchGroups(group => group
        .Key(i => i.PayoutId, p => p.Reference)
        .Amount(i => i.Amount, p => p.NetAmount, tolerance: 2_000.00m))
    .Run();

foreach (var match in result.MatchedGroups)
{
    Console.WriteLine(
        $"{match.Right.Reference}: {match.Lefts.Count} invoices, " +
        $"gross {match.LeftTotal}, net {match.RightAmount}, fees {match.AmountVariance}");
}
// PAYOUT-114: 3 invoices, gross 100000.00, net 98500.00, fees 1500.00

public sealed record Invoice(string Reference, string PayoutId, decimal Amount);

public sealed record Payout(string Reference, decimal NetAmount, DateTime PaidAt);
```

The group key is explicit: your ledger rows already know which payout they belong to (or you derive it before reconciling). The engine does not guess which subset of rows sums to a payout; see the roadmap for why.

Ordering matters operationally: declare group passes before any loose amount-tolerance pass. A pass runs on everything still unmatched, so a generous keyless or amount-tolerance pass declared first can steal a single invoice out of a batch, and the payout group then fails to sum. Strict key passes first, group passes next, loose tolerance passes last.

## Aging what is left

Unmatched items are the output that matters. Age them straight off the result:

```csharp
using System;
using Reconcile.Net;

var ledger = new[]
{
    new LedgerRow("INV-1008", 4_500.00m, new DateTime(2026, 6, 1)),
    new LedgerRow("INV-1009", 900.00m, new DateTime(2026, 7, 20)),
    new LedgerRow("INV-1010", 2_750.00m, new DateTime(2026, 4, 15)),
};

var result = Reconciliation
    .Between(ledger, Array.Empty<StatementRow>())
    .MatchOn(l => l.Reference, r => r.Reference)
    .Run();

var aging = result.AgeUnmatchedLeft(l => l.PostedAt, asOf: new DateTime(2026, 8, 3));

foreach (var bucket in aging.Buckets)
{
    Console.WriteLine($"{bucket.Label} days: {bucket.Items.Count}");
}
// 0-7 days: 0
// 8-30 days: 1
// 31-90 days: 1
// 91+ days: 1

public sealed record LedgerRow(string Reference, decimal Amount, DateTime PostedAt);

public sealed record StatementRow(string Reference, decimal Amount, DateTime ValueDate);
```

Ages are whole calendar days relative to `asOf`; future-dated items count as zero. `AgeUnmatchedRight` does the same for the other side, and both accept custom bucket bounds: `result.AgeUnmatchedLeft(l => l.PostedAt, asOf, new[] { 30, 60 })` gives 0-30, 31-60, 61+.

Aging compares calendar dates only: the `DateTimeKind` of the dates your selector returns and of `asOf` is ignored, exactly as it is for the date-window match criteria. Normalize both sides to one timezone before aging so a record posted late in the day in one zone does not land a bucket early in another.

## Determinism promises

Reconciliation output ends up in audit trails, so the resolution rules are explicit and tested, not incidental:

1. Passes run in the order you declare them. Each pass sees only the records every earlier pass left unmatched.
2. Every record is matched at most once, across all passes, whether as a pair or inside a group.
3. Left records are evaluated in their original stream order. When several candidates satisfy a pass equally, the first candidate in original right-stream order wins. In group passes, right records claim groups in original right-stream order.
4. Result order is stable: `Matched` is ordered by pass then by original left order, `MatchedGroups` by pass then by original right order, and the unmatched lists preserve original stream order.
5. The same input and the same rules produce the same output, every run. There is no randomness and no dependence on hash iteration order.
6. Null never equals null. A record whose key selector returns null (any part of a composite key) never key-matches on that pass: blank-reference rows never silently pair with each other, they stay available to later passes and otherwise surface in the exception lists where they belong.
7. Input order is part of the input. Matching is greedy in stream order, not globally optimal: re-sorting a file can change which records match, and when tolerances overlap it can even change how many (we measured 164 to 170 matches across 50 shuffles of one overlapping-tolerance dataset). If you need order-independent runs, pre-sort both sides canonically (for example by reference, then date) before calling `Between`. A globally optimal assignment mode is on the roadmap.

All boundaries are inclusive: an amount difference exactly equal to the tolerance matches, and a date gap exactly equal to the window matches.

Money is `decimal` end to end. There is no `float` or `double` anywhere in an amount path.

## Performance

Keyed passes build a dictionary over one side and stream the other: O(n + m) per pass, not O(n x m). The test suite includes a timed proof: 100,000 ledger rows against 100,000 shuffled statement rows fully match in about 120 ms on an ordinary laptop, and doubling both sides to 200,000 x 200,000 scales elapsed time by about 2x (quadratic behavior would be 4x).

Passes without any key (amount or date criteria only) have no dictionary to lean on and degrade to comparing each remaining left record against remaining candidates; keep at least one `Key` in passes that run over large remainders.

To turn that trap into a loud, catchable error, set an opt-in ceiling on how large an unkeyed pass may run:

```csharp
var result = Reconciliation
    .Between(ledger, statement)
    .WithOptions(new ReconciliationOptions { MaxUnkeyedPassProduct = 1_000_000 })
    .MatchOn(l => l.Reference, r => r.Reference)
    .ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount, tolerance: 1m))
    .Run();
```

Before an unkeyed pass runs, the engine measures the product of the remaining left and right counts (after every earlier pass has trimmed the remainder). If that product exceeds `MaxUnkeyedPassProduct`, `Run` throws an `InvalidOperationException` naming the pass and the offending size instead of grinding through the comparisons. It is off by default (`null`), so leaving it unset preserves the original unbounded behavior.

## Limitations and roadmap

Honest constraints in 0.1:

- Group matching needs an explicit group key on both sides. There is no subset-sum discovery ("which of these 40 rows sum to this payout"); that problem is exponential in the worst case and a silent performance trap, so it will only ever ship behind explicit limits. It is on the roadmap in exactly that form.
- The summary's amount-bearing totals (`AmountBearingMatchedLeftTotal`, `AmountBearingMatchedRightTotal`, `AmountBearingVarianceTotal`) cover only matches made by passes that declare an amount criterion; a key-only pass never reads amounts, so its matches cannot contribute there. For true totals across every match and everything unmatched, call `result.SumAmounts(l => l.Amount, r => r.Amount)`: its matched plus unmatched totals always equal the input totals on each side.
- Date criteria take `DateTime`. Convert `DateOnly` or `DateTimeOffset` in your selectors for now.
- Everything is in-memory and synchronous. Streams are enumerated exactly once per `Run`.

Roadmap: bounded subset-sum discovery for group passes, a globally optimal assignment mode (order-independent matching), camt.053 / MT940 / CSV statement adapters as a separate package, streaming `IAsyncEnumerable` input, `DateOnly` and `DateTimeOffset` date criteria, a value-type composite key path for typed keys to avoid boxing, `netstandard2.0` target.

## License

MIT. See [LICENSE](LICENSE).
