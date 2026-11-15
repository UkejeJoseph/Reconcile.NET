using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Grouping;

public class GroupGreedyClaimTests
{
    [Fact]
    public void First_right_in_order_claims_the_shared_group_even_when_a_later_right_fits_better()
    {
        var ledger = new[]
        {
            Rows.Ledger("INV-1", 10_000m, payoutId: "PAY"),
            Rows.Ledger("INV-2", 20_000m, payoutId: "PAY"),
            Rows.Ledger("INV-3", 30_000m, payoutId: "PAY"),
        };
        var statement = new[]
        {
            Rows.Statement("PAY", 59_500m, accountRef: "first"),
            Rows.Statement("PAY", 60_000m, accountRef: "second"),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 1_000m))
            .Run();

        var matched = Assert.Single(result.MatchedGroups);
        Assert.Equal("first", matched.Right.AccountRef);
        Assert.Equal(60_000m, matched.LeftTotal);
        Assert.Equal(500m, matched.AmountVariance);
        Assert.Equal("second", Assert.Single(result.UnmatchedRight).AccountRef);
        Assert.Empty(result.UnmatchedLeft);
    }
}
