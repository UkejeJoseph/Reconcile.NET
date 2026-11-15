using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Scenarios;

public class SettlementStoryTests
{
    private static readonly DateTime AsOf = Rows.ParseDate("2026-08-03");

    private static readonly LedgerRow[] Ledger =
    {
        new("INV-1001", "C1", 5_000.00m, Rows.ParseDate("2026-07-01")),
        new("INV-1002", "C2", 12_500.50m, Rows.ParseDate("2026-07-02")),
        new("INV-1003", "C3", 8_000.00m, Rows.ParseDate("2026-07-03")),
        new("INV-1004", "C4", 20_000.00m, Rows.ParseDate("2026-07-05")),
        new("INV-1005", "C9", 3_000.00m, Rows.ParseDate("2026-07-06"), "PAY-88"),
        new("INV-1006", "C9", 7_000.00m, Rows.ParseDate("2026-07-06"), "PAY-88"),
        new("INV-1007", "C9", 10_000.00m, Rows.ParseDate("2026-07-07"), "PAY-88"),
        new("INV-1008", "C5", 4_500.00m, Rows.ParseDate("2026-06-01")),
        new("INV-1009", "C6", 900.00m, Rows.ParseDate("2026-07-20")),
        new("INV-1010", "C7", 2_750.00m, Rows.ParseDate("2026-04-15")),
    };

    private static readonly StatementRow[] Statement =
    {
        new("INV-1001", "C1", 5_000.00m, Rows.ParseDate("2026-07-01")),
        new("INV-1002", "C2", 12_500.50m, Rows.ParseDate("2026-07-03")),
        new("TRF/00234", "C3", 8_000.00m, Rows.ParseDate("2026-07-04")),
        new("TRF/00235", "C4", 19_999.50m, Rows.ParseDate("2026-07-06")),
        new("PAY-88", "-", 19_900.00m, Rows.ParseDate("2026-07-08")),
        new("BANK-CHG", "-", 50.00m, Rows.ParseDate("2026-07-31")),
        new("INV-9999", "C8", 1_234.00m, Rows.ParseDate("2026-07-15")),
    };

    private static ReconciliationResult<LedgerRow, StatementRow> Reconcile()
        => Reconciliation
            .Between(Ledger, Statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchOn(pass => pass
                .Key(l => l.CustomerId, r => r.AccountRef)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 1.00m)
                .Date(l => l.PostedAt, r => r.ValueDate, TimeSpan.FromDays(2), DateWindowDirection.RightMayBeLater))
            .ThenMatchGroups(group => group
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: 150.00m))
            .Run();

    [Fact]
    public void Pass_one_matches_exact_references()
    {
        var result = Reconcile();

        var passOne = result.Matched.Where(p => p.PassNumber == 1).ToArray();
        Assert.Equal(new[] { "INV-1001", "INV-1002" }, passOne.Select(p => p.Left.Reference));
        Assert.All(passOne, p => Assert.Equal(p.Left.Reference, p.Right.Reference));
    }

    [Fact]
    public void Pass_two_recovers_mangled_references_by_customer_amount_and_date()
    {
        var result = Reconcile();

        var passTwo = result.Matched.Where(p => p.PassNumber == 2).ToArray();
        Assert.Equal(new[] { "INV-1003", "INV-1004" }, passTwo.Select(p => p.Left.Reference));
        Assert.Equal(new[] { "TRF/00234", "TRF/00235" }, passTwo.Select(p => p.Right.Reference));
        Assert.Equal(0.00m, passTwo[0].AmountVariance);
        Assert.Equal(0.50m, passTwo[1].AmountVariance);
    }

    [Fact]
    public void Pass_three_matches_the_net_payout_against_its_three_invoices()
    {
        var result = Reconcile();

        var payout = Assert.Single(result.MatchedGroups);
        Assert.Equal(3, payout.PassNumber);
        Assert.Equal("PAY-88", payout.Right.Reference);
        Assert.Equal(
            new[] { "INV-1005", "INV-1006", "INV-1007" },
            payout.Lefts.Select(l => l.Reference));
        Assert.Equal(20_000.00m, payout.LeftTotal);
        Assert.Equal(19_900.00m, payout.RightAmount);
        Assert.Equal(100.00m, payout.AmountVariance);
    }

    [Fact]
    public void Leftovers_are_reported_on_both_sides_in_original_order()
    {
        var result = Reconcile();

        Assert.Equal(
            new[] { "INV-1008", "INV-1009", "INV-1010" },
            result.UnmatchedLeft.Select(l => l.Reference));
        Assert.Equal(
            new[] { "BANK-CHG", "INV-9999" },
            result.UnmatchedRight.Select(r => r.Reference));
    }

    [Fact]
    public void Summary_accounts_for_every_record_and_every_naira()
    {
        var summary = Reconcile().Summary;

        Assert.Equal(4, summary.MatchedPairCount);
        Assert.Equal(1, summary.MatchedGroupCount);
        Assert.Equal(7, summary.MatchedLeftCount);
        Assert.Equal(5, summary.MatchedRightCount);
        Assert.Equal(3, summary.UnmatchedLeftCount);
        Assert.Equal(2, summary.UnmatchedRightCount);
        Assert.Equal(48_000.00m, summary.AmountBearingMatchedLeftTotal);
        Assert.Equal(47_899.50m, summary.AmountBearingMatchedRightTotal);
        Assert.Equal(100.50m, summary.AmountBearingVarianceTotal);
    }

    [Fact]
    public void SumAmounts_reports_true_totals_across_every_pass_and_conserves_input()
    {
        var totals = Reconcile().SumAmounts(l => l.Amount, r => r.Amount);

        Assert.Equal(65_500.50m, totals.MatchedLeftTotal);
        Assert.Equal(65_400.00m, totals.MatchedRightTotal);
        Assert.Equal(100.50m, totals.Variance);
        Assert.Equal(8_150.00m, totals.UnmatchedLeftTotal);
        Assert.Equal(1_284.00m, totals.UnmatchedRightTotal);
        Assert.Equal(Ledger.Sum(l => l.Amount), totals.MatchedLeftTotal + totals.UnmatchedLeftTotal);
        Assert.Equal(Statement.Sum(r => r.Amount), totals.MatchedRightTotal + totals.UnmatchedRightTotal);
    }

    [Fact]
    public void Unmatched_ledger_rows_age_into_the_expected_buckets()
    {
        var report = Reconcile().AgeUnmatchedLeft(l => l.PostedAt, AsOf);

        Assert.Empty(report.Buckets[0].Items);
        Assert.Equal("INV-1009", Assert.Single(report.Buckets[1].Items).Reference);
        Assert.Equal("INV-1008", Assert.Single(report.Buckets[2].Items).Reference);
        Assert.Equal("INV-1010", Assert.Single(report.Buckets[3].Items).Reference);
        Assert.Equal(3, report.TotalCount);
    }
}
