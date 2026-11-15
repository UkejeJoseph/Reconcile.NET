using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Matching;

public class MultiPassTests
{
    [Fact]
    public void Passes_run_in_declaration_order_and_stamp_their_pass_number()
    {
        var ledger = new[]
        {
            Rows.Ledger("INV-1", 100m, customerId: "C1"),
            Rows.Ledger("INV-2", 200m, customerId: "C2"),
        };
        var statement = new[]
        {
            Rows.Statement("INV-1", 100m, accountRef: "C1"),
            Rows.Statement("MANGLED", 200m, accountRef: "C2"),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchOn(pass => pass
                .Key(l => l.CustomerId, r => r.AccountRef)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Equal(2, result.Matched.Count);
        Assert.Equal(1, result.Matched[0].PassNumber);
        Assert.Equal("INV-1", result.Matched[0].Left.Reference);
        Assert.Equal(2, result.Matched[1].PassNumber);
        Assert.Equal("INV-2", result.Matched[1].Left.Reference);
    }

    [Fact]
    public void Later_passes_only_see_records_unmatched_by_earlier_passes()
    {
        var ledger = new[] { Rows.Ledger("A", 100m) };
        var statement = new[]
        {
            Rows.Statement("A", 100m),
            Rows.Statement("B", 100m),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount))
            .Run();

        var pair = Assert.Single(result.Matched);
        Assert.Equal(1, pair.PassNumber);
        Assert.Equal("A", pair.Right.Reference);
        Assert.Equal("B", Assert.Single(result.UnmatchedRight).Reference);
    }

    [Fact]
    public void Every_record_is_matched_at_most_once_across_passes()
    {
        var ledger = new[]
        {
            Rows.Ledger("A", 100m),
            Rows.Ledger("A", 100m),
            Rows.Ledger("A", 100m),
        };
        var statement = new[]
        {
            Rows.Statement("A", 100m),
            Rows.Statement("A", 100m),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Equal(2, result.Matched.Count);
        Assert.Single(result.UnmatchedLeft);
        Assert.Empty(result.UnmatchedRight);
        Assert.Equal(ledger.Length, result.Matched.Count + result.UnmatchedLeft.Count);
        Assert.Equal(statement.Length, result.Matched.Count + result.UnmatchedRight.Count);
    }

    [Fact]
    public void Keyless_amount_pass_matches_on_amount_alone()
    {
        var ledger = new[] { Rows.Ledger("L-REF", 750.25m) };
        var statement = new[] { Rows.Statement("S-REF", 750.25m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Single(result.Matched);
    }

    [Fact]
    public void Pass_description_names_the_criteria()
    {
        var result = Reconciliation
            .Between(new[] { Rows.Ledger("A", 1m) }, new[] { Rows.Statement("A", 1m) })
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 0.01m)
                .Date(l => l.PostedAt, r => r.ValueDate, TimeSpan.FromDays(2)))
            .Run();

        var pair = Assert.Single(result.Matched);
        Assert.Contains("Key", pair.PassDescription);
        Assert.Contains("Amount", pair.PassDescription);
        Assert.Contains("Date", pair.PassDescription);
    }
}
