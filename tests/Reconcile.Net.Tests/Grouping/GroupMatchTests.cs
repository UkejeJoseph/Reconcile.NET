using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Grouping;

public class GroupMatchTests
{
    private static LedgerRow[] ThreeInvoicesForPayout(string payoutId = "PAY-88")
        => new[]
        {
            Rows.Ledger("INV-1", 10_000m, payoutId: payoutId),
            Rows.Ledger("INV-2", 20_000m, payoutId: payoutId),
            Rows.Ledger("INV-3", 30_000m, payoutId: payoutId),
        };

    [Fact]
    public void Payout_net_of_fees_matches_the_sum_of_its_invoices_within_tolerance()
    {
        var ledger = ThreeInvoicesForPayout();
        var statement = new[] { Rows.Statement("PAY-88", 59_100m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 1_000m))
            .Run();

        var matched = Assert.Single(result.MatchedGroups);
        Assert.Equal(3, matched.Lefts.Count);
        Assert.Equal(60_000m, matched.LeftTotal);
        Assert.Equal(59_100m, matched.RightAmount);
        Assert.Equal(900m, matched.AmountVariance);
        Assert.Empty(result.UnmatchedLeft);
        Assert.Empty(result.UnmatchedRight);
    }

    [Fact]
    public void Group_sum_outside_tolerance_leaves_everything_unmatched()
    {
        var ledger = ThreeInvoicesForPayout();
        var statement = new[] { Rows.Statement("PAY-88", 58_000m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 1_000m))
            .Run();

        Assert.Empty(result.MatchedGroups);
        Assert.Equal(3, result.UnmatchedLeft.Count);
        Assert.Single(result.UnmatchedRight);
    }

    [Fact]
    public void Group_tolerance_boundary_is_inclusive()
    {
        var ledger = ThreeInvoicesForPayout();
        var statement = new[] { Rows.Statement("PAY-88", 59_000m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 1_000m))
            .Run();

        Assert.Single(result.MatchedGroups);
    }

    [Fact]
    public void Group_percent_tolerance_is_relative_to_the_right_amount()
    {
        var ledger = ThreeInvoicesForPayout();
        var statement = new[] { Rows.Statement("PAY-88", 59_100m) };

        var withinPercent = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .AmountPercent(l => l.Amount, r => r.Amount, tolerancePercent: 2m))
            .Run();
        var outsidePercent = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .AmountPercent(l => l.Amount, r => r.Amount, tolerancePercent: 1m))
            .Run();

        Assert.Single(withinPercent.MatchedGroups);
        Assert.Empty(outsidePercent.MatchedGroups);
    }

    [Fact]
    public void Second_right_with_the_same_group_key_stays_unmatched()
    {
        var ledger = ThreeInvoicesForPayout();
        var statement = new[]
        {
            Rows.Statement("PAY-88", 60_000m, accountRef: "first"),
            Rows.Statement("PAY-88", 60_000m, accountRef: "second"),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        var matched = Assert.Single(result.MatchedGroups);
        Assert.Equal("first", matched.Right.AccountRef);
        Assert.Equal("second", Assert.Single(result.UnmatchedRight).AccountRef);
    }

    [Fact]
    public void Lefts_matched_by_an_earlier_pass_are_excluded_from_the_group_sum()
    {
        var ledger = ThreeInvoicesForPayout();
        var statement = new[]
        {
            Rows.Statement("INV-1", 10_000m),
            Rows.Statement("PAY-88", 50_000m),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Single(result.Matched);
        var matchedGroup = Assert.Single(result.MatchedGroups);
        Assert.Equal(2, matchedGroup.Lefts.Count);
        Assert.Equal(50_000m, matchedGroup.LeftTotal);
        Assert.Empty(result.UnmatchedLeft);
        Assert.Empty(result.UnmatchedRight);
    }

    [Fact]
    public void Group_matched_records_are_excluded_from_later_passes()
    {
        var ledger = ThreeInvoicesForPayout();
        var statement = new[]
        {
            Rows.Statement("PAY-88", 60_000m),
            Rows.Statement("INV-1", 10_000m),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .ThenMatchOn(pass => pass.Key(l => l.Reference, r => r.Reference))
            .Run();

        Assert.Single(result.MatchedGroups);
        Assert.Empty(result.Matched);
        Assert.Equal("INV-1", Assert.Single(result.UnmatchedRight).Reference);
    }

    [Fact]
    public void Group_of_a_single_left_record_matches()
    {
        var ledger = new[] { Rows.Ledger("INV-9", 5_000m, payoutId: "PAY-1") };
        var statement = new[] { Rows.Statement("PAY-1", 5_000m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        var matched = Assert.Single(result.MatchedGroups);
        Assert.Single(matched.Lefts);
        Assert.Equal(0m, matched.AmountVariance);
    }

    [Fact]
    public void Right_with_an_unknown_group_key_stays_unmatched()
    {
        var ledger = ThreeInvoicesForPayout("PAY-88");
        var statement = new[] { Rows.Statement("PAY-99", 60_000m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Empty(result.MatchedGroups);
        Assert.Equal(3, result.UnmatchedLeft.Count);
        Assert.Single(result.UnmatchedRight);
    }

    [Fact]
    public void Rights_claim_groups_in_original_right_order()
    {
        var ledger = new[]
        {
            Rows.Ledger("INV-1", 100m, payoutId: "P1"),
            Rows.Ledger("INV-2", 200m, payoutId: "P2"),
        };
        var statement = new[]
        {
            Rows.Statement("P2", 200m),
            Rows.Statement("P1", 100m),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Equal(2, result.MatchedGroups.Count);
        Assert.Equal("P2", result.MatchedGroups[0].Right.Reference);
        Assert.Equal("P1", result.MatchedGroups[1].Right.Reference);
    }

    [Fact]
    public void Group_pass_description_mentions_grouping()
    {
        var ledger = new[] { Rows.Ledger("INV-1", 100m, payoutId: "P1") };
        var statement = new[] { Rows.Statement("P1", 100m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        var matched = Assert.Single(result.MatchedGroups);
        Assert.Contains("Group", matched.PassDescription);
    }
}
