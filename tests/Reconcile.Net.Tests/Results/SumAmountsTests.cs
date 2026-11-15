using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Results;

public class SumAmountsTests
{
    private static readonly LedgerRow[] Ledger =
    {
        Rows.Ledger("A", 100.00m),
        Rows.Ledger("B", 250.50m),
        new("INV-1", "C1", 10_000m, Rows.ParseDate("2026-07-01"), "P1"),
        new("INV-2", "C1", 20_000m, Rows.ParseDate("2026-07-01"), "P1"),
        Rows.Ledger("MISS-L", 77.77m),
    };

    private static readonly StatementRow[] Statement =
    {
        Rows.Statement("A", 100.00m),
        Rows.Statement("B", 250.49m),
        Rows.Statement("P1", 29_700m),
        Rows.Statement("MISS-R", 12.34m),
    };

    private static ReconciliationResult<LedgerRow, StatementRow> Reconcile()
        => Reconciliation
            .Between(Ledger, Statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 500m))
            .Run();

    [Fact]
    public void SumAmounts_totals_every_match_including_key_only_pairs_and_groups()
    {
        var totals = Reconcile().SumAmounts(l => l.Amount, r => r.Amount);

        Assert.Equal(100.00m + 250.50m + 30_000m, totals.MatchedLeftTotal);
        Assert.Equal(100.00m + 250.49m + 29_700m, totals.MatchedRightTotal);
        Assert.Equal(300.01m, totals.Variance);
        Assert.Equal(77.77m, totals.UnmatchedLeftTotal);
        Assert.Equal(12.34m, totals.UnmatchedRightTotal);
    }

    [Fact]
    public void SumAmounts_conserves_the_input_totals_on_both_sides()
    {
        var totals = Reconcile().SumAmounts(l => l.Amount, r => r.Amount);

        Assert.Equal(Ledger.Sum(l => l.Amount), totals.MatchedLeftTotal + totals.UnmatchedLeftTotal);
        Assert.Equal(Statement.Sum(r => r.Amount), totals.MatchedRightTotal + totals.UnmatchedRightTotal);
    }

    [Fact]
    public void SumAmounts_variance_is_matched_left_minus_matched_right()
    {
        var totals = Reconcile().SumAmounts(l => l.Amount, r => r.Amount);

        Assert.Equal(totals.MatchedLeftTotal - totals.MatchedRightTotal, totals.Variance);
    }

    [Fact]
    public void SumAmounts_on_an_empty_result_returns_zeroes()
    {
        var totals = Reconciliation
            .Between(Array.Empty<LedgerRow>(), Array.Empty<StatementRow>())
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run()
            .SumAmounts(l => l.Amount, r => r.Amount);

        Assert.Equal(0m, totals.MatchedLeftTotal);
        Assert.Equal(0m, totals.MatchedRightTotal);
        Assert.Equal(0m, totals.Variance);
        Assert.Equal(0m, totals.UnmatchedLeftTotal);
        Assert.Equal(0m, totals.UnmatchedRightTotal);
    }

    [Fact]
    public void SumAmounts_throws_for_null_selectors()
    {
        var result = Reconcile();
        Assert.Throws<ArgumentNullException>(() => result.SumAmounts(null!, r => r.Amount));
        Assert.Throws<ArgumentNullException>(() => result.SumAmounts(l => l.Amount, null!));
    }
}
