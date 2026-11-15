using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Api;

public class BuilderValidationTests
{
    private static readonly LedgerRow[] SomeLedger = { Rows.Ledger("A", 100m) };
    private static readonly StatementRow[] SomeStatement = { Rows.Statement("A", 100m) };

    private static ReconciliationBuilder<LedgerRow, StatementRow> Builder()
        => Reconciliation.Between(SomeLedger, SomeStatement);

    [Fact]
    public void Between_throws_for_null_left()
    {
        Assert.Throws<ArgumentNullException>(
            () => Reconciliation.Between<LedgerRow, StatementRow>(null!, SomeStatement));
    }

    [Fact]
    public void Between_throws_for_null_right()
    {
        Assert.Throws<ArgumentNullException>(
            () => Reconciliation.Between<LedgerRow, StatementRow>(SomeLedger, null!));
    }

    [Fact]
    public void MatchOn_throws_for_null_left_selector()
    {
        Assert.Throws<ArgumentNullException>(
            () => Builder().MatchOn<string>(null!, r => r.Reference));
    }

    [Fact]
    public void MatchOn_throws_for_null_right_selector()
    {
        Assert.Throws<ArgumentNullException>(
            () => Builder().MatchOn<string>(l => l.Reference, null!));
    }

    [Fact]
    public void MatchOn_after_ThenMatchOn_throws()
    {
        var builder = Builder().ThenMatchOn(pass => pass.Key(l => l.Reference, r => r.Reference));
        Assert.Throws<InvalidOperationException>(
            () => builder.MatchOn(l => l.Reference, r => r.Reference));
    }

    [Fact]
    public void MatchOn_after_ThenMatchGroups_throws()
    {
        var builder = Builder().ThenMatchGroups(pass => pass
            .Key(l => l.PayoutId, r => r.Reference)
            .Amount(l => l.Amount, r => r.Amount));
        Assert.Throws<InvalidOperationException>(
            () => builder.MatchOn(l => l.Reference, r => r.Reference));
    }

    [Fact]
    public void ThenMatchOn_throws_for_null_configure()
    {
        Assert.Throws<ArgumentNullException>(() => Builder().ThenMatchOn(null!));
    }

    [Fact]
    public void ThenMatchOn_with_no_criteria_throws()
    {
        Assert.Throws<InvalidOperationException>(() => Builder().ThenMatchOn(pass => { }));
    }

    [Fact]
    public void ThenMatchGroups_throws_for_null_configure()
    {
        Assert.Throws<ArgumentNullException>(() => Builder().ThenMatchGroups(null!));
    }

    [Fact]
    public void ThenMatchGroups_without_key_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => Builder().ThenMatchGroups(pass => pass.Amount(l => l.Amount, r => r.Amount)));
    }

    [Fact]
    public void ThenMatchGroups_without_amount_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => Builder().ThenMatchGroups(pass => pass.Key(l => l.PayoutId, r => r.Reference)));
    }

    [Fact]
    public void Run_without_passes_throws()
    {
        Assert.Throws<InvalidOperationException>(() => Builder().Run());
    }

    [Fact]
    public void Pass_Key_throws_for_null_selectors()
    {
        Assert.Throws<ArgumentNullException>(
            () => Builder().ThenMatchOn(pass => pass.Key<string>(null!, r => r.Reference)));
        Assert.Throws<ArgumentNullException>(
            () => Builder().ThenMatchOn(pass => pass.Key<string>(l => l.Reference, null!)));
    }

    [Fact]
    public void Pass_Amount_throws_for_null_selectors()
    {
        Assert.Throws<ArgumentNullException>(
            () => Builder().ThenMatchOn(pass => pass.Amount(null!, r => r.Amount)));
        Assert.Throws<ArgumentNullException>(
            () => Builder().ThenMatchOn(pass => pass.Amount(l => l.Amount, null!)));
    }

    [Fact]
    public void Pass_Amount_throws_for_negative_tolerance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Builder().ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount, tolerance: -0.01m)));
    }

    [Fact]
    public void Pass_AmountPercent_throws_for_negative_tolerance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Builder().ThenMatchOn(pass => pass.AmountPercent(l => l.Amount, r => r.Amount, tolerancePercent: -1m)));
    }

    [Fact]
    public void Pass_with_two_amount_criteria_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => Builder().ThenMatchOn(pass => pass
                .Amount(l => l.Amount, r => r.Amount)
                .AmountPercent(l => l.Amount, r => r.Amount, tolerancePercent: 1m)));
    }

    [Fact]
    public void Pass_Date_throws_for_null_selectors()
    {
        Assert.Throws<ArgumentNullException>(
            () => Builder().ThenMatchOn(pass => pass.Date(null!, r => r.ValueDate, TimeSpan.Zero)));
        Assert.Throws<ArgumentNullException>(
            () => Builder().ThenMatchOn(pass => pass.Date(l => l.PostedAt, null!, TimeSpan.Zero)));
    }

    [Fact]
    public void Pass_Date_throws_for_negative_window()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Builder().ThenMatchOn(pass => pass.Date(l => l.PostedAt, r => r.ValueDate, TimeSpan.FromDays(-1))));
    }

    [Fact]
    public void Pass_Date_throws_for_undefined_direction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Builder().ThenMatchOn(pass => pass.Date(
                l => l.PostedAt, r => r.ValueDate, TimeSpan.Zero, (DateWindowDirection)99)));
    }

    [Fact]
    public void Pass_with_two_date_criteria_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => Builder().ThenMatchOn(pass => pass
                .Date(l => l.PostedAt, r => r.ValueDate, TimeSpan.Zero)
                .Date(l => l.PostedAt, r => r.ValueDate, TimeSpan.Zero)));
    }

    [Fact]
    public void Group_Key_throws_for_null_selectors()
    {
        Assert.Throws<ArgumentNullException>(
            () => Builder().ThenMatchGroups(pass => pass.Key<string>(null!, r => r.Reference)));
        Assert.Throws<ArgumentNullException>(
            () => Builder().ThenMatchGroups(pass => pass.Key<string>(l => l.PayoutId, null!)));
    }

    [Fact]
    public void Group_Amount_throws_for_null_selectors()
    {
        Assert.Throws<ArgumentNullException>(
            () => Builder().ThenMatchGroups(pass => pass.Amount(null!, r => r.Amount)));
        Assert.Throws<ArgumentNullException>(
            () => Builder().ThenMatchGroups(pass => pass.Amount(l => l.Amount, null!)));
    }

    [Fact]
    public void Group_Amount_throws_for_negative_tolerance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Builder().ThenMatchGroups(pass => pass
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount, tolerance: -1m)));
    }

    [Fact]
    public void Group_AmountPercent_throws_for_negative_tolerance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Builder().ThenMatchGroups(pass => pass
                .Key(l => l.PayoutId, r => r.Reference)
                .AmountPercent(l => l.Amount, r => r.Amount, tolerancePercent: -1m)));
    }

    [Fact]
    public void Group_with_two_amount_criteria_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => Builder().ThenMatchGroups(pass => pass
                .Key(l => l.PayoutId, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount)
                .AmountPercent(l => l.Amount, r => r.Amount, tolerancePercent: 1m)));
    }

    [Fact]
    public void Fluent_calls_return_the_same_builder()
    {
        var builder = Builder();
        Assert.Same(builder, builder.MatchOn(l => l.Reference, r => r.Reference));
        Assert.Same(builder, builder.ThenMatchOn(pass => pass.Key(l => l.CustomerId, r => r.AccountRef)));
    }
}
