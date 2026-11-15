using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Matching;

public class NullKeyTests
{
    [Fact]
    public void Null_reference_never_matches_null_reference()
    {
        var ledger = new[]
        {
            new LedgerRow(null!, "C1", 100m, Rows.ParseDate("2026-07-01")),
            new LedgerRow(null!, "C2", 5_000m, Rows.ParseDate("2026-07-02")),
        };
        var statement = new[]
        {
            new StatementRow(null!, "C1", 999m, Rows.ParseDate("2026-07-01")),
            new StatementRow(null!, "C2", 42m, Rows.ParseDate("2026-07-02")),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Empty(result.Matched);
        Assert.Equal(2, result.UnmatchedLeft.Count);
        Assert.Equal(2, result.UnmatchedRight.Count);
    }

    [Fact]
    public void Composite_key_with_one_null_part_never_matches()
    {
        var ledger = new[] { new LedgerRow("REF", null!, 100m, Rows.ParseDate("2026-07-01")) };
        var statement = new[] { new StatementRow("REF", null!, 100m, Rows.ParseDate("2026-07-01")) };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .MatchOn(l => l.CustomerId, r => r.AccountRef)
            .Run();

        Assert.Empty(result.Matched);
        Assert.Single(result.UnmatchedLeft);
        Assert.Single(result.UnmatchedRight);
    }

    [Fact]
    public void Non_null_key_never_matches_a_null_key_on_the_other_side()
    {
        var ledger = new[] { Rows.Ledger("A", 100m) };
        var statement = new[] { new StatementRow(null!, "C1", 100m, Rows.ParseDate("2026-07-01")) };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Empty(result.Matched);
    }

    [Fact]
    public void Null_key_record_is_still_matchable_by_a_later_pass_with_non_null_keys()
    {
        var ledger = new[] { new LedgerRow(null!, "C9", 500m, Rows.ParseDate("2026-07-01")) };
        var statement = new[] { new StatementRow(null!, "C9", 500m, Rows.ParseDate("2026-07-01")) };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchOn(pass => pass
                .Key(l => l.CustomerId, r => r.AccountRef)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        var pair = Assert.Single(result.Matched);
        Assert.Equal(2, pair.PassNumber);
    }

    [Fact]
    public void Null_group_keys_never_group_and_never_match()
    {
        var ledger = new[]
        {
            new LedgerRow("INV-1", "C1", 100m, Rows.ParseDate("2026-07-01"), PayoutId: null!),
            new LedgerRow("INV-2", "C1", 200m, Rows.ParseDate("2026-07-01"), PayoutId: null!),
        };
        var statement = new[] { new StatementRow(null!, "C1", 300m, Rows.ParseDate("2026-07-02")) };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Empty(result.MatchedGroups);
        Assert.Equal(2, result.UnmatchedLeft.Count);
        Assert.Single(result.UnmatchedRight);
    }

    [Fact]
    public void Null_payout_lefts_do_not_pollute_a_real_group()
    {
        var ledger = new[]
        {
            new LedgerRow("INV-1", "C1", 100m, Rows.ParseDate("2026-07-01"), "PAY-1"),
            new LedgerRow("INV-2", "C1", 900m, Rows.ParseDate("2026-07-01"), PayoutId: null!),
        };
        var statement = new[] { Rows.Statement("PAY-1", 100m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        var matched = Assert.Single(result.MatchedGroups);
        Assert.Equal("INV-1", Assert.Single(matched.Lefts).Reference);
        Assert.Equal("INV-2", Assert.Single(result.UnmatchedLeft).Reference);
    }
}
