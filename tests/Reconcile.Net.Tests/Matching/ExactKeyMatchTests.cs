using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Matching;

public class ExactKeyMatchTests
{
    [Fact]
    public void Single_key_matches_records_with_equal_references()
    {
        var ledger = new[] { Rows.Ledger("INV-1", 100m), Rows.Ledger("INV-2", 200m) };
        var statement = new[] { Rows.Statement("INV-2", 200m), Rows.Statement("INV-1", 100m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Equal(2, result.Matched.Count);
        Assert.Empty(result.UnmatchedLeft);
        Assert.Empty(result.UnmatchedRight);
        Assert.All(result.Matched, pair => Assert.Equal(pair.Left.Reference, pair.Right.Reference));
    }

    [Fact]
    public void Unmatched_records_are_preserved_in_original_order()
    {
        var ledger = new[]
        {
            Rows.Ledger("A", 1m),
            Rows.Ledger("X1", 2m),
            Rows.Ledger("B", 3m),
            Rows.Ledger("X2", 4m),
        };
        var statement = new[]
        {
            Rows.Statement("Y1", 5m),
            Rows.Statement("A", 1m),
            Rows.Statement("Y2", 6m),
            Rows.Statement("B", 3m),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Equal(new[] { "X1", "X2" }, result.UnmatchedLeft.Select(l => l.Reference));
        Assert.Equal(new[] { "Y1", "Y2" }, result.UnmatchedRight.Select(r => r.Reference));
    }

    [Fact]
    public void Composite_key_requires_every_part_to_match()
    {
        var ledger = new[]
        {
            Rows.Ledger("REF", 100m, customerId: "C1"),
            Rows.Ledger("REF", 100m, customerId: "C2"),
        };
        var statement = new[]
        {
            Rows.Statement("REF", 100m, accountRef: "C2"),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .MatchOn(l => l.CustomerId, r => r.AccountRef)
            .Run();

        var pair = Assert.Single(result.Matched);
        Assert.Equal("C2", pair.Left.CustomerId);
        Assert.Equal("C1", Assert.Single(result.UnmatchedLeft).CustomerId);
    }

    [Fact]
    public void Duplicate_references_pair_in_original_order_on_both_sides()
    {
        var ledger = new[]
        {
            Rows.Ledger("DUP", 10m, customerId: "L-first"),
            Rows.Ledger("DUP", 20m, customerId: "L-second"),
        };
        var statement = new[]
        {
            Rows.Statement("DUP", 30m, accountRef: "R-first"),
            Rows.Statement("DUP", 40m, accountRef: "R-second"),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Equal(2, result.Matched.Count);
        Assert.Equal("R-first", result.Matched[0].Right.AccountRef);
        Assert.Equal("L-first", result.Matched[0].Left.CustomerId);
        Assert.Equal("R-second", result.Matched[1].Right.AccountRef);
        Assert.Equal("L-second", result.Matched[1].Left.CustomerId);
    }

    [Fact]
    public void Same_amount_same_day_transactions_do_not_cross_match_when_keys_differ()
    {
        var ledger = new[]
        {
            Rows.Ledger("T-100", 5000m, customerId: "ALICE", postedAt: "2026-07-10"),
            Rows.Ledger("T-200", 5000m, customerId: "BOB", postedAt: "2026-07-10"),
        };
        var statement = new[]
        {
            Rows.Statement("S-1", 5000m, accountRef: "BOB", valueDate: "2026-07-10"),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchOn(pass => pass
                .Key(l => l.CustomerId, r => r.AccountRef)
                .Amount(l => l.Amount, r => r.Amount)
                .Date(l => l.PostedAt, r => r.ValueDate, TimeSpan.Zero))
            .Run();

        var pair = Assert.Single(result.Matched);
        Assert.Equal("BOB", pair.Left.CustomerId);
        Assert.Equal("ALICE", Assert.Single(result.UnmatchedLeft).CustomerId);
    }

    [Fact]
    public void Empty_streams_produce_an_empty_result()
    {
        var result = Reconciliation
            .Between(Array.Empty<LedgerRow>(), Array.Empty<StatementRow>())
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Empty(result.Matched);
        Assert.Empty(result.MatchedGroups);
        Assert.Empty(result.UnmatchedLeft);
        Assert.Empty(result.UnmatchedRight);
        Assert.Equal(0, result.Summary.MatchedPairCount);
    }

    [Fact]
    public void Left_only_stream_leaves_everything_unmatched_left()
    {
        var ledger = new[] { Rows.Ledger("A", 1m), Rows.Ledger("B", 2m) };

        var result = Reconciliation
            .Between(ledger, Array.Empty<StatementRow>())
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Empty(result.Matched);
        Assert.Equal(2, result.UnmatchedLeft.Count);
        Assert.Empty(result.UnmatchedRight);
    }

    [Fact]
    public void Right_only_stream_leaves_everything_unmatched_right()
    {
        var statement = new[] { Rows.Statement("A", 1m), Rows.Statement("B", 2m) };

        var result = Reconciliation
            .Between(Array.Empty<LedgerRow>(), statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Empty(result.Matched);
        Assert.Empty(result.UnmatchedLeft);
        Assert.Equal(2, result.UnmatchedRight.Count);
    }

    [Fact]
    public void Key_only_pass_records_no_amount_or_date_variance()
    {
        var result = Reconciliation
            .Between(new[] { Rows.Ledger("A", 1m) }, new[] { Rows.Statement("A", 2m) })
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        var pair = Assert.Single(result.Matched);
        Assert.Null(pair.AmountVariance);
        Assert.Null(pair.DateVariance);
    }
}
