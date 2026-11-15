using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Api;

public class UnkeyedPassGuardTests
{
    private static LedgerRow[] Ledger(int count)
        => Enumerable.Range(0, count).Select(i => Rows.Ledger($"L{i}", i + 1)).ToArray();

    private static StatementRow[] Statement(int count)
        => Enumerable.Range(0, count).Select(i => Rows.Statement($"R{i}", i + 1)).ToArray();

    [Fact]
    public void Unkeyed_pass_over_the_configured_product_throws()
    {
        var ledger = Ledger(3);
        var statement = Statement(3);

        var ex = Assert.Throws<InvalidOperationException>(() => Reconciliation
            .Between(ledger, statement)
            .WithOptions(new ReconciliationOptions { MaxUnkeyedPassProduct = 8 })
            .ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount, tolerance: 100m))
            .Run());

        Assert.Contains(nameof(ReconciliationOptions.MaxUnkeyedPassProduct), ex.Message);
    }

    [Fact]
    public void Unkeyed_pass_at_or_under_the_configured_product_runs()
    {
        var ledger = Ledger(2);
        var statement = Statement(2);

        var result = Reconciliation
            .Between(ledger, statement)
            .WithOptions(new ReconciliationOptions { MaxUnkeyedPassProduct = 4 })
            .ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Equal(2, result.Matched.Count);
    }

    [Fact]
    public void Unkeyed_pass_is_unlimited_by_default()
    {
        var ledger = Ledger(20);
        var statement = Statement(20);

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Equal(20, result.Matched.Count);
    }

    [Fact]
    public void Unlimited_when_the_option_is_left_null()
    {
        var ledger = Ledger(20);
        var statement = Statement(20);

        var result = Reconciliation
            .Between(ledger, statement)
            .WithOptions(new ReconciliationOptions { MaxUnkeyedPassProduct = null })
            .ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Equal(20, result.Matched.Count);
    }

    [Fact]
    public void Keyed_pass_is_never_limited_by_the_unkeyed_guard()
    {
        var ledger = Enumerable.Range(0, 50).Select(i => Rows.Ledger($"REF{i}", i + 1)).ToArray();
        var statement = Enumerable.Range(0, 50).Select(i => Rows.Statement($"REF{i}", i + 1)).ToArray();

        var result = Reconciliation
            .Between(ledger, statement)
            .WithOptions(new ReconciliationOptions { MaxUnkeyedPassProduct = 1 })
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Equal(50, result.Matched.Count);
    }

    [Fact]
    public void Guard_measures_the_remaining_product_after_earlier_passes()
    {
        var ledger = new[]
        {
            Rows.Ledger("SAME1", 1m),
            Rows.Ledger("SAME2", 2m),
            Rows.Ledger("LX3", 3m),
            Rows.Ledger("LX4", 4m),
        };
        var statement = new[]
        {
            Rows.Statement("SAME1", 1m),
            Rows.Statement("SAME2", 2m),
            Rows.Statement("RX3", 3m),
            Rows.Statement("RX4", 4m),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .WithOptions(new ReconciliationOptions { MaxUnkeyedPassProduct = 4 })
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Equal(4, result.Matched.Count);
        Assert.Empty(result.UnmatchedLeft);
    }
}
