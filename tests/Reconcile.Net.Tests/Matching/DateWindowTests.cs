using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Matching;

public class DateWindowTests
{
    private static ReconciliationResult<LedgerRow, StatementRow> Run(
        string postedAt, string valueDate, TimeSpan window, DateWindowDirection direction)
        => Reconciliation
            .Between(
                new[] { Rows.Ledger("A", 100m, postedAt: postedAt) },
                new[] { Rows.Statement("A", 100m, valueDate: valueDate) })
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Date(l => l.PostedAt, r => r.ValueDate, window, direction))
            .Run();

    public static TheoryData<string, string, int, DateWindowDirection, bool> WindowCases => new()
    {
        { "2026-07-10", "2026-07-10", 0, DateWindowDirection.Either, true },
        { "2026-07-12", "2026-07-10", 2, DateWindowDirection.Either, true },
        { "2026-07-08", "2026-07-10", 2, DateWindowDirection.Either, true },
        { "2026-07-13", "2026-07-10", 2, DateWindowDirection.Either, false },
        { "2026-07-07", "2026-07-10", 2, DateWindowDirection.Either, false },
        { "2026-07-12", "2026-07-10", 2, DateWindowDirection.LeftMayBeLater, true },
        { "2026-07-10", "2026-07-10", 2, DateWindowDirection.LeftMayBeLater, true },
        { "2026-07-09", "2026-07-10", 2, DateWindowDirection.LeftMayBeLater, false },
        { "2026-07-13", "2026-07-10", 2, DateWindowDirection.LeftMayBeLater, false },
        { "2026-07-08", "2026-07-10", 2, DateWindowDirection.RightMayBeLater, true },
        { "2026-07-10", "2026-07-10", 2, DateWindowDirection.RightMayBeLater, true },
        { "2026-07-11", "2026-07-10", 2, DateWindowDirection.RightMayBeLater, false },
        { "2026-07-07", "2026-07-10", 2, DateWindowDirection.RightMayBeLater, false },
    };

    [Theory]
    [MemberData(nameof(WindowCases))]
    public void Date_window_boundary_is_inclusive_and_direction_aware(
        string postedAt, string valueDate, int windowDays, DateWindowDirection direction, bool expectMatch)
    {
        var result = Run(postedAt, valueDate, TimeSpan.FromDays(windowDays), direction);
        Assert.Equal(expectMatch ? 1 : 0, result.Matched.Count);
    }

    [Fact]
    public void One_second_past_the_window_does_not_match()
    {
        var result = Run("2026-07-12 00:00:01", "2026-07-10", TimeSpan.FromDays(2), DateWindowDirection.Either);
        Assert.Empty(result.Matched);
    }

    [Fact]
    public void Date_overload_without_direction_allows_either_side_to_lag()
    {
        var result = Reconciliation
            .Between(
                new[] { Rows.Ledger("A", 100m, postedAt: "2026-07-08") },
                new[] { Rows.Statement("A", 100m, valueDate: "2026-07-10") })
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Date(l => l.PostedAt, r => r.ValueDate, window: TimeSpan.FromDays(2)))
            .Run();

        Assert.Single(result.Matched);
    }

    [Fact]
    public void Matched_pair_records_signed_date_variance_as_left_minus_right()
    {
        var result = Run("2026-07-12", "2026-07-10", TimeSpan.FromDays(2), DateWindowDirection.Either);

        var pair = Assert.Single(result.Matched);
        Assert.Equal(TimeSpan.FromDays(2), pair.DateVariance);
    }
}
