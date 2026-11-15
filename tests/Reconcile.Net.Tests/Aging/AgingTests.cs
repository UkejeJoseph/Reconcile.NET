using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Aging;

public class AgingTests
{
    private static readonly DateTime AsOf = Rows.ParseDate("2026-08-03");

    private static ReconciliationResult<LedgerRow, StatementRow> ResultWithUnmatchedLedger(
        params LedgerRow[] ledger)
        => Reconciliation
            .Between(ledger, Array.Empty<StatementRow>())
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

    public static TheoryData<int, string> DefaultBucketCases => new()
    {
        { 0, "0-7" },
        { 7, "0-7" },
        { 8, "8-30" },
        { 30, "8-30" },
        { 31, "31-90" },
        { 90, "31-90" },
        { 91, "91+" },
        { 400, "91+" },
    };

    [Theory]
    [MemberData(nameof(DefaultBucketCases))]
    public void Default_bucket_boundaries_are_inclusive_upper_bounds(int ageDays, string expectedLabel)
    {
        var postedAt = AsOf.AddDays(-ageDays);
        var result = ResultWithUnmatchedLedger(new LedgerRow("A", "C1", 100m, postedAt));

        var report = result.AgeUnmatchedLeft(l => l.PostedAt, AsOf);

        var bucket = Assert.Single(report.Buckets, b => b.Items.Count > 0);
        Assert.Equal(expectedLabel, bucket.Label);
    }

    [Fact]
    public void Default_report_has_four_buckets_with_documented_ranges()
    {
        var result = ResultWithUnmatchedLedger(Rows.Ledger("A", 100m));
        var report = result.AgeUnmatchedLeft(l => l.PostedAt, AsOf);

        Assert.Equal(4, report.Buckets.Count);
        Assert.Equal(new[] { "0-7", "8-30", "31-90", "91+" }, report.Buckets.Select(b => b.Label));
        Assert.Equal(0, report.Buckets[0].FromDays);
        Assert.Equal(7, report.Buckets[0].ToDays);
        Assert.Equal(8, report.Buckets[1].FromDays);
        Assert.Equal(30, report.Buckets[1].ToDays);
        Assert.Equal(31, report.Buckets[2].FromDays);
        Assert.Equal(90, report.Buckets[2].ToDays);
        Assert.Equal(91, report.Buckets[3].FromDays);
        Assert.Null(report.Buckets[3].ToDays);
    }

    [Fact]
    public void Future_dated_items_land_in_the_first_bucket()
    {
        var result = ResultWithUnmatchedLedger(new LedgerRow("A", "C1", 100m, AsOf.AddDays(5)));

        var report = result.AgeUnmatchedLeft(l => l.PostedAt, AsOf);

        Assert.Single(report.Buckets[0].Items);
    }

    [Fact]
    public void Age_is_measured_in_calendar_days()
    {
        var postedAt = Rows.ParseDate("2026-08-02 23:59:00");
        var asOf = Rows.ParseDate("2026-08-03 00:01:00");
        var result = ResultWithUnmatchedLedger(new LedgerRow("A", "C1", 100m, postedAt));

        var report = result.AgeUnmatchedLeft(l => l.PostedAt, asOf);

        Assert.Single(report.Buckets[0].Items);
        Assert.Equal(1, report.TotalCount);
    }

    [Fact]
    public void Custom_bucket_bounds_produce_custom_labels_and_ranges()
    {
        var result = ResultWithUnmatchedLedger(
            new LedgerRow("A", "C1", 1m, AsOf.AddDays(-10)),
            new LedgerRow("B", "C1", 2m, AsOf.AddDays(-45)),
            new LedgerRow("C", "C1", 3m, AsOf.AddDays(-100)));

        var report = result.AgeUnmatchedLeft(l => l.PostedAt, AsOf, new[] { 30, 60 });

        Assert.Equal(new[] { "0-30", "31-60", "61+" }, report.Buckets.Select(b => b.Label));
        Assert.Equal("A", Assert.Single(report.Buckets[0].Items).Reference);
        Assert.Equal("B", Assert.Single(report.Buckets[1].Items).Reference);
        Assert.Equal("C", Assert.Single(report.Buckets[2].Items).Reference);
    }

    [Fact]
    public void Buckets_preserve_original_record_order_and_total_count_sums_items()
    {
        var result = ResultWithUnmatchedLedger(
            new LedgerRow("FIRST", "C1", 1m, AsOf.AddDays(-1)),
            new LedgerRow("SECOND", "C1", 2m, AsOf.AddDays(-2)),
            new LedgerRow("OLD", "C1", 3m, AsOf.AddDays(-200)));

        var report = result.AgeUnmatchedLeft(l => l.PostedAt, AsOf);

        Assert.Equal(new[] { "FIRST", "SECOND" }, report.Buckets[0].Items.Select(l => l.Reference));
        Assert.Equal(3, report.TotalCount);
    }

    [Fact]
    public void AgeUnmatchedRight_ages_the_right_stream()
    {
        var statement = new[]
        {
            new StatementRow("S1", "C1", 100m, AsOf.AddDays(-40)),
        };
        var result = Reconciliation
            .Between(Array.Empty<LedgerRow>(), statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        var report = result.AgeUnmatchedRight(r => r.ValueDate, AsOf);

        Assert.Equal("S1", Assert.Single(report.Buckets[2].Items).Reference);
    }

    [Fact]
    public void Aging_throws_for_null_selector()
    {
        var result = ResultWithUnmatchedLedger(Rows.Ledger("A", 1m));
        Assert.Throws<ArgumentNullException>(() => result.AgeUnmatchedLeft(null!, AsOf));
        Assert.Throws<ArgumentNullException>(() => result.AgeUnmatchedRight(null!, AsOf));
    }

    [Fact]
    public void Aging_throws_for_null_custom_bounds()
    {
        var result = ResultWithUnmatchedLedger(Rows.Ledger("A", 1m));
        Assert.Throws<ArgumentNullException>(
            () => result.AgeUnmatchedLeft(l => l.PostedAt, AsOf, null!));
    }

    public static TheoryData<int[]> InvalidBounds => new()
    {
        Array.Empty<int>(),
        new[] { 0 },
        new[] { -5 },
        new[] { 30, 30 },
        new[] { 60, 30 },
    };

    [Theory]
    [MemberData(nameof(InvalidBounds))]
    public void Aging_throws_for_invalid_custom_bounds(int[] bounds)
    {
        var result = ResultWithUnmatchedLedger(Rows.Ledger("A", 1m));
        Assert.Throws<ArgumentException>(
            () => result.AgeUnmatchedLeft(l => l.PostedAt, AsOf, bounds));
    }
}
