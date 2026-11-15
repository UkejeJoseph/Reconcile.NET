using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Matching;

public class AtMostOnceTests
{
    [Fact]
    public void Record_eligible_for_both_a_pair_pass_and_a_later_group_pass_is_claimed_only_once()
    {
        var ledger = new[]
        {
            Rows.Ledger("INV-1", 100m, payoutId: "PAY"),
            Rows.Ledger("INV-2", 200m, payoutId: "PAY"),
        };
        var statement = new[]
        {
            Rows.Statement("INV-1", 100m),
            Rows.Statement("PAY", 200m),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        var pair = Assert.Single(result.Matched);
        Assert.Equal("INV-1", pair.Left.Reference);

        var group = Assert.Single(result.MatchedGroups);
        Assert.Equal("INV-2", Assert.Single(group.Lefts).Reference);
        Assert.DoesNotContain(group.Lefts, l => l.Reference == "INV-1");

        var inv1Appearances =
            result.Matched.Count(p => p.Left.Reference == "INV-1")
            + result.MatchedGroups.SelectMany(g => g.Lefts).Count(l => l.Reference == "INV-1")
            + result.UnmatchedLeft.Count(l => l.Reference == "INV-1");
        Assert.Equal(1, inv1Appearances);

        Assert.Empty(result.UnmatchedLeft);
        Assert.Empty(result.UnmatchedRight);
    }
}
