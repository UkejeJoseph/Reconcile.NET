using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Matching;

public class NegativeAndRefundTests
{
    [Fact]
    public void Refund_matches_refund_on_key_and_amount_with_signed_variance()
    {
        var ledger = new[]
        {
            Rows.Ledger("RF-1", -500.00m),
            Rows.Ledger("RF-2", -1_250.75m),
        };
        var statement = new[]
        {
            Rows.Statement("RF-1", -500.00m),
            Rows.Statement("RF-2", -1_250.50m),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 0.50m))
            .Run();

        Assert.Equal(2, result.Matched.Count);
        var exact = result.Matched.Single(p => p.Left.Reference == "RF-1");
        var near = result.Matched.Single(p => p.Left.Reference == "RF-2");
        Assert.Equal(0m, exact.AmountVariance);
        Assert.Equal(-0.25m, near.AmountVariance);

        var totals = result.SumAmounts(l => l.Amount, r => r.Amount);
        Assert.Equal(-1_750.75m, totals.MatchedLeftTotal);
        Assert.Equal(0m, totals.UnmatchedLeftTotal);
    }

    [Fact]
    public void A_charge_and_its_refund_do_not_match_on_amount_despite_equal_magnitude()
    {
        var ledger = new[] { Rows.Ledger("TX-9", 500.00m) };
        var statement = new[] { Rows.Statement("TX-9", -500.00m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Empty(result.Matched);
        Assert.Single(result.UnmatchedLeft);
        Assert.Single(result.UnmatchedRight);
    }

    [Fact]
    public void Amount_percent_tolerance_uses_the_magnitude_of_a_negative_right_amount()
    {
        var ledger = new[]
        {
            Rows.Ledger("RF-IN", -1_010.00m),
            Rows.Ledger("RF-OUT", -1_030.00m),
        };
        var statement = new[]
        {
            Rows.Statement("RF-IN", -1_000.00m),
            Rows.Statement("RF-OUT", -1_000.00m),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .AmountPercent(l => l.Amount, r => r.Amount, tolerancePercent: 2m))
            .Run();

        var matched = Assert.Single(result.Matched);
        Assert.Equal("RF-IN", matched.Left.Reference);
        Assert.Equal(-10.00m, matched.AmountVariance);
        Assert.Equal("RF-OUT", Assert.Single(result.UnmatchedLeft).Reference);
    }
}
