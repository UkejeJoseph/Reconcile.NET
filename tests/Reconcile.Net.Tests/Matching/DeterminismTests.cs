using Reconcile.Net.Tests.Support;

namespace Reconcile.Net.Tests.Matching;

public class DeterminismTests
{
    [Fact]
    public void Tie_between_equal_candidates_is_resolved_by_original_right_order()
    {
        var ledger = new[] { Rows.Ledger("X", 100m) };
        var statement = new[]
        {
            Rows.Statement("X", 100m, accountRef: "first"),
            Rows.Statement("X", 100m, accountRef: "second"),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Equal("first", Assert.Single(result.Matched).Right.AccountRef);
        Assert.Equal("second", Assert.Single(result.UnmatchedRight).AccountRef);
    }

    [Fact]
    public void Left_records_claim_candidates_in_original_left_order()
    {
        var ledger = new[]
        {
            Rows.Ledger("X", 100m, customerId: "first-left"),
            Rows.Ledger("X", 100m, customerId: "second-left"),
        };
        var statement = new[] { Rows.Statement("X", 100m) };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .Run();

        Assert.Equal("first-left", Assert.Single(result.Matched).Left.CustomerId);
        Assert.Equal("second-left", Assert.Single(result.UnmatchedLeft).CustomerId);
    }

    [Fact]
    public void Candidate_failing_criteria_is_skipped_for_the_next_in_original_order()
    {
        var ledger = new[]
        {
            Rows.Ledger("X", 200m, customerId: "L1"),
            Rows.Ledger("X", 100m, customerId: "L2"),
        };
        var statement = new[]
        {
            Rows.Statement("X", 100m, accountRef: "R1"),
            Rows.Statement("X", 200m, accountRef: "R2"),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

        Assert.Equal(2, result.Matched.Count);
        Assert.Equal("R2", result.Matched.Single(p => p.Left.CustomerId == "L1").Right.AccountRef);
        Assert.Equal("R1", result.Matched.Single(p => p.Left.CustomerId == "L2").Right.AccountRef);
    }

    [Fact]
    public void Matched_list_is_ordered_by_pass_then_by_original_left_order()
    {
        var ledger = new[]
        {
            Rows.Ledger("B", 2m, customerId: "C2"),
            Rows.Ledger("A", 1m, customerId: "C1"),
            Rows.Ledger("MISS", 3m, customerId: "C3"),
        };
        var statement = new[]
        {
            Rows.Statement("A", 1m, accountRef: "C1"),
            Rows.Statement("B", 2m, accountRef: "C2"),
            Rows.Statement("OTHER", 3m, accountRef: "C3"),
        };

        var result = Reconciliation
            .Between(ledger, statement)
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchOn(pass => pass.Key(l => l.CustomerId, r => r.AccountRef))
            .Run();

        Assert.Equal(new[] { "B", "A", "MISS" }, result.Matched.Select(p => p.Left.Reference));
        Assert.Equal(new[] { 1, 1, 2 }, result.Matched.Select(p => p.PassNumber));
    }

    [Fact]
    public void Running_the_same_reconciliation_twice_yields_identical_results()
    {
        var random = new Random(42);
        var ledger = Enumerable.Range(0, 500)
            .Select(i => Rows.Ledger($"R{random.Next(0, 200)}", random.Next(1, 50)))
            .ToArray();
        var statement = Enumerable.Range(0, 500)
            .Select(i => Rows.Statement($"R{random.Next(0, 200)}", random.Next(1, 50)))
            .ToArray();

        ReconciliationResult<LedgerRow, StatementRow> Run()
            => Reconciliation
                .Between(ledger, statement)
                .MatchOn(l => l.Reference, r => r.Reference)
                .ThenMatchOn(pass => pass.Amount(l => l.Amount, r => r.Amount, tolerance: 1m))
                .Run();

        var first = Run();
        var second = Run();

        Assert.Equal(
            first.Matched.Select(p => (p.Left, p.Right, p.PassNumber)),
            second.Matched.Select(p => (p.Left, p.Right, p.PassNumber)));
        Assert.Equal(first.UnmatchedLeft, second.UnmatchedLeft);
        Assert.Equal(first.UnmatchedRight, second.UnmatchedRight);
    }
}
