using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Matching;

public class AmountToleranceTests
{
    private static ReconciliationResult<LedgerRow, StatementRow> RunAbsolute(
        decimal leftAmount, decimal rightAmount, decimal tolerance)
        => Reconciliation
            .Between(new[] { Rows.Ledger("A", leftAmount) }, new[] { Rows.Statement("A", rightAmount) })
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance))
            .Run();

    private static ReconciliationResult<LedgerRow, StatementRow> RunPercent(
        decimal leftAmount, decimal rightAmount, decimal tolerancePercent)
        => Reconciliation
            .Between(new[] { Rows.Ledger("A", leftAmount) }, new[] { Rows.Statement("A", rightAmount) })
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .AmountPercent(l => l.Amount, r => r.Amount, tolerancePercent))
            .Run();

    public static TheoryData<decimal, decimal, decimal, bool> AbsoluteToleranceCases => new()
    {
        { 100.00m, 100.00m, 0.00m, true },
        { 100.00m, 100.01m, 0.00m, false },
        { 100.00m, 100.01m, 0.01m, true },
        { 100.00m, 99.99m, 0.01m, true },
        { 100.00m, 100.02m, 0.01m, false },
        { 100.00m, 99.98m, 0.01m, false },
        { 4500.07m, 4500.07m, 0.00m, true },
        { 4500.07m, 4500.08m, 0.00m, false },
        { 9999999999.99m, 9999999999.98m, 0.01m, true },
        { 0.01m, 0.02m, 0.01m, true },
    };

    [Theory]
    [MemberData(nameof(AbsoluteToleranceCases))]
    public void Absolute_tolerance_boundary_is_inclusive(
        decimal leftAmount, decimal rightAmount, decimal tolerance, bool expectMatch)
    {
        var result = RunAbsolute(leftAmount, rightAmount, tolerance);
        Assert.Equal(expectMatch ? 1 : 0, result.Matched.Count);
    }

    public static TheoryData<decimal, decimal, decimal, bool> PercentToleranceCases => new()
    {
        { 100.50m, 100.00m, 0.5m, true },
        { 100.51m, 100.00m, 0.5m, false },
        { 99.50m, 100.00m, 0.5m, true },
        { 99.49m, 100.00m, 0.5m, false },
        { 0.00m, 0.00m, 1.0m, true },
        { 0.01m, 0.00m, 5.0m, false },
        { 995.00m, 1000.00m, 0.5m, true },
        { 1000.00m, 995.00m, 0.5m, false },
    };

    [Theory]
    [MemberData(nameof(PercentToleranceCases))]
    public void Percent_tolerance_is_relative_to_the_right_amount_and_inclusive(
        decimal leftAmount, decimal rightAmount, decimal tolerancePercent, bool expectMatch)
    {
        var result = RunPercent(leftAmount, rightAmount, tolerancePercent);
        Assert.Equal(expectMatch ? 1 : 0, result.Matched.Count);
    }

    [Fact]
    public void Amount_without_tolerance_defaults_to_exact()
    {
        var result = Reconciliation
            .Between(new[] { Rows.Ledger("A", 250.00m) }, new[] { Rows.Statement("A", 250.001m) })
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Empty(result.Matched);
    }

    [Fact]
    public void Matched_pair_records_signed_amount_variance_as_left_minus_right()
    {
        var result = RunAbsolute(100.00m, 100.01m, 0.05m);

        var pair = Assert.Single(result.Matched);
        Assert.Equal(-0.01m, pair.AmountVariance);
    }
}
