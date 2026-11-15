using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Results;

public class SummaryTests
{
    [Fact]
    public void Counts_cover_every_record_exactly_once()
    {
        var ledger = new[]
        {
            Rows.Ledger("A", 100m),
            Rows.Ledger("B", 200m),
            Rows.Ledger("MISS-L", 300m),
        };
        var statement = new[]
        {
            Rows.Statement("A", 100m),
            Rows.Statement("B", 200m),
            Rows.Statement("MISS-R1", 400m),
            Rows.Statement("MISS-R2", 500m),
        };

        var summary = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run()
            .Summary;

        Assert.Equal(2, summary.MatchedPairCount);
        Assert.Equal(0, summary.MatchedGroupCount);
        Assert.Equal(2, summary.MatchedLeftCount);
        Assert.Equal(2, summary.MatchedRightCount);
        Assert.Equal(1, summary.UnmatchedLeftCount);
        Assert.Equal(2, summary.UnmatchedRightCount);
        Assert.Equal(ledger.Length, summary.MatchedLeftCount + summary.UnmatchedLeftCount);
        Assert.Equal(statement.Length, summary.MatchedRightCount + summary.UnmatchedRightCount);
    }

    [Fact]
    public void Totals_accumulate_amounts_for_amount_bearing_passes()
    {
        var ledger = new[]
        {
            Rows.Ledger("A", 100.00m),
            Rows.Ledger("B", 200.00m),
        };
        var statement = new[]
        {
            Rows.Statement("A", 99.99m),
            Rows.Statement("B", 200.02m),
        };

        var summary = Reconciliation
            .Between(ledger, statement)
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 0.05m))
            .Run()
            .Summary;

        Assert.Equal(300.00m, summary.AmountBearingMatchedLeftTotal);
        Assert.Equal(300.01m, summary.AmountBearingMatchedRightTotal);
        Assert.Equal(-0.01m, summary.AmountBearingVarianceTotal);
    }

    [Fact]
    public void Key_only_passes_contribute_counts_but_not_amount_totals()
    {
        var summary = Reconciliation
            .Between(new[] { Rows.Ledger("A", 100m) }, new[] { Rows.Statement("A", 100m) })
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run()
            .Summary;

        Assert.Equal(1, summary.MatchedPairCount);
        Assert.Equal(0m, summary.AmountBearingMatchedLeftTotal);
        Assert.Equal(0m, summary.AmountBearingMatchedRightTotal);
        Assert.Equal(0m, summary.AmountBearingVarianceTotal);
    }

    [Fact]
    public void Group_matches_contribute_group_totals_and_counts()
    {
        var ledger = new[]
        {
            Rows.Ledger("INV-1", 10_000m, payoutId: "P1"),
            Rows.Ledger("INV-2", 20_000m, payoutId: "P1"),
        };
        var statement = new[] { Rows.Statement("P1", 29_700m) };

        var summary = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 500m))
            .Run()
            .Summary;

        Assert.Equal(0, summary.MatchedPairCount);
        Assert.Equal(1, summary.MatchedGroupCount);
        Assert.Equal(2, summary.MatchedLeftCount);
        Assert.Equal(1, summary.MatchedRightCount);
        Assert.Equal(30_000m, summary.AmountBearingMatchedLeftTotal);
        Assert.Equal(29_700m, summary.AmountBearingMatchedRightTotal);
        Assert.Equal(300m, summary.AmountBearingVarianceTotal);
    }

    [Fact]
    public void Variance_total_is_signed_and_nets_across_matches()
    {
        var ledger = new[]
        {
            Rows.Ledger("A", 100.00m),
            Rows.Ledger("B", 199.98m),
        };
        var statement = new[]
        {
            Rows.Statement("A", 100.01m),
            Rows.Statement("B", 199.97m),
        };

        var summary = Reconciliation
            .Between(ledger, statement)
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 0.05m))
            .Run()
            .Summary;

        Assert.Equal(0.00m, summary.AmountBearingVarianceTotal);
    }
}
