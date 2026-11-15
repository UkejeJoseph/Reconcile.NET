namespace Reconcile.Net.Tests.Properties;

/// <summary>
/// Deterministic-seeded fuzz testing of the conservation, determinism, and consistency invariants
/// over hundreds of randomized datasets. Each dataset mixes negatives and refunds, zeros, kobo
/// precision, shared group keys, orphans, near-matches, and null keys. Fixed seeds keep every run
/// reproducible, so a failure names the exact dataset and never flakes in CI.
/// </summary>
public class ConservationPropertyTests
{
    private const int SeedCount = 250;
    private const int MinRecords = 6;
    private const int MaxRecords = 30;
    private const int RefPoolSize = 8;
    private const int AltRefPoolSize = 8;
    private const int PayoutPoolSize = 5;
    private const int NullInNParts = 7;
    private const int ZeroInNAmounts = 10;
    private const int MinKobo = -100_000;
    private const int MaxKoboExclusive = 100_001;
    private const decimal KoboPerUnit = 100m;
    private const int MaxAgeDays = 45;
    private const decimal NearMatchTolerance = 0.10m;
    private const decimal GroupTolerance = 0.01m;
    private const int DateWindowDays = 3;

    private static readonly DateTime BaseDate = new(2026, 1, 1);

    private sealed record FuzzLeft(
        int Id, string? Reference, string? AltRef, string? Payout, decimal Amount, DateTime Date);

    private sealed record FuzzRight(
        int Id, string? Reference, string? AltRef, string? Payout, decimal Amount, DateTime Date);

    public static IEnumerable<object[]> Seeds =>
        Enumerable.Range(1, SeedCount).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Every_dataset_conserves_records_amounts_and_stays_deterministic(int seed)
    {
        var (left, right) = BuildDataset(seed);

        var result = Reconcile(left, right);
        AssertConservesRecords(left, right, result);
        AssertConservesAmounts(left, right, result);
        AssertSummaryIsConsistent(left, right, result);

        var rerun = Reconcile(left, right);
        AssertIdenticalOutput(result, rerun);

        var (shuffledLeft, shuffledRight) = Shuffle(left, right, seed);
        var shuffledResult = Reconcile(shuffledLeft, shuffledRight);
        AssertConservesRecords(shuffledLeft, shuffledRight, shuffledResult);
        AssertConservesAmounts(shuffledLeft, shuffledRight, shuffledResult);
        AssertSummaryIsConsistent(shuffledLeft, shuffledRight, shuffledResult);
    }

    [Fact]
    public void The_fuzz_corpus_exercises_the_intended_variety()
    {
        var totalPairs = 0;
        var totalGroups = 0;
        var totalUnmatchedLeft = 0;
        var totalUnmatchedRight = 0;
        var sawNegative = false;
        var sawZero = false;
        var sawNullKey = false;
        var sawGroupOfManyLefts = false;

        for (var seed = 1; seed <= SeedCount; seed++)
        {
            var (left, right) = BuildDataset(seed);
            var result = Reconcile(left, right);

            totalPairs += result.Matched.Count;
            totalGroups += result.MatchedGroups.Count;
            totalUnmatchedLeft += result.UnmatchedLeft.Count;
            totalUnmatchedRight += result.UnmatchedRight.Count;
            sawNegative |= left.Any(l => l.Amount < 0m) || right.Any(r => r.Amount < 0m);
            sawZero |= left.Any(l => l.Amount == 0m) || right.Any(r => r.Amount == 0m);
            sawNullKey |= left.Any(l => l.Reference is null) || right.Any(r => r.Reference is null);
            sawGroupOfManyLefts |= result.MatchedGroups.Any(g => g.Lefts.Count > 1);
        }

        Assert.True(totalPairs > 0, "Expected some one-to-one matches across the corpus.");
        Assert.True(totalGroups > 0, "Expected some group matches across the corpus.");
        Assert.True(totalUnmatchedLeft > 0, "Expected some unmatched left records across the corpus.");
        Assert.True(totalUnmatchedRight > 0, "Expected some unmatched right records across the corpus.");
        Assert.True(sawNegative, "Expected negative amounts in the corpus.");
        Assert.True(sawZero, "Expected zero amounts in the corpus.");
        Assert.True(sawNullKey, "Expected null keys in the corpus.");
        Assert.True(sawGroupOfManyLefts, "Expected at least one multi-left group match.");
    }

    private static ReconciliationResult<FuzzLeft, FuzzRight> Reconcile(FuzzLeft[] left, FuzzRight[] right)
        => Reconciliation
            .Between(left, right)
            .MatchOn(l => l.Reference, r => r.Reference)
            .ThenMatchOn(pass => pass
                .Key(l => l.AltRef, r => r.AltRef)
                .Amount(l => l.Amount, r => r.Amount, tolerance: NearMatchTolerance))
            .ThenMatchOn(pass => pass
                .Key(l => l.AltRef, r => r.AltRef)
                .Date(l => l.Date, r => r.Date, TimeSpan.FromDays(DateWindowDays)))
            .ThenMatchGroups(group => group
                .Key(l => l.Payout, r => r.Payout)
                .Amount(l => l.Amount, r => r.Amount, tolerance: GroupTolerance))
            .Run();

    private static void AssertConservesRecords(
        FuzzLeft[] left, FuzzRight[] right, ReconciliationResult<FuzzLeft, FuzzRight> result)
    {
        var leftIds = result.Matched.Select(p => p.Left.Id)
            .Concat(result.MatchedGroups.SelectMany(g => g.Lefts).Select(l => l.Id))
            .Concat(result.UnmatchedLeft.Select(l => l.Id))
            .OrderBy(id => id)
            .ToArray();
        Assert.Equal(left.Select(l => l.Id).OrderBy(id => id), leftIds);

        var rightIds = result.Matched.Select(p => p.Right.Id)
            .Concat(result.MatchedGroups.Select(g => g.Right.Id))
            .Concat(result.UnmatchedRight.Select(r => r.Id))
            .OrderBy(id => id)
            .ToArray();
        Assert.Equal(right.Select(r => r.Id).OrderBy(id => id), rightIds);
    }

    private static void AssertConservesAmounts(
        FuzzLeft[] left, FuzzRight[] right, ReconciliationResult<FuzzLeft, FuzzRight> result)
    {
        var totals = result.SumAmounts(l => l.Amount, r => r.Amount);
        Assert.Equal(left.Sum(l => l.Amount), totals.MatchedLeftTotal + totals.UnmatchedLeftTotal);
        Assert.Equal(right.Sum(r => r.Amount), totals.MatchedRightTotal + totals.UnmatchedRightTotal);
        Assert.Equal(totals.MatchedLeftTotal - totals.MatchedRightTotal, totals.Variance);
    }

    private static void AssertSummaryIsConsistent(
        FuzzLeft[] left, FuzzRight[] right, ReconciliationResult<FuzzLeft, FuzzRight> result)
    {
        var summary = result.Summary;
        Assert.Equal(result.Matched.Count, summary.MatchedPairCount);
        Assert.Equal(result.MatchedGroups.Count, summary.MatchedGroupCount);
        Assert.Equal(result.UnmatchedLeft.Count, summary.UnmatchedLeftCount);
        Assert.Equal(result.UnmatchedRight.Count, summary.UnmatchedRightCount);

        var matchedLefts = result.Matched.Count + result.MatchedGroups.Sum(g => g.Lefts.Count);
        var matchedRights = result.Matched.Count + result.MatchedGroups.Count;
        Assert.Equal(matchedLefts, summary.MatchedLeftCount);
        Assert.Equal(matchedRights, summary.MatchedRightCount);
        Assert.Equal(left.Length, summary.MatchedLeftCount + summary.UnmatchedLeftCount);
        Assert.Equal(right.Length, summary.MatchedRightCount + summary.UnmatchedRightCount);
    }

    private static void AssertIdenticalOutput(
        ReconciliationResult<FuzzLeft, FuzzRight> first, ReconciliationResult<FuzzLeft, FuzzRight> second)
    {
        Assert.Equal(
            first.Matched.Select(p => (p.Left.Id, p.Right.Id, p.PassNumber)),
            second.Matched.Select(p => (p.Left.Id, p.Right.Id, p.PassNumber)));
        Assert.Equal(
            first.MatchedGroups.Select(g => (string.Join(",", g.Lefts.Select(l => l.Id)), g.Right.Id)),
            second.MatchedGroups.Select(g => (string.Join(",", g.Lefts.Select(l => l.Id)), g.Right.Id)));
        Assert.Equal(first.UnmatchedLeft.Select(l => l.Id), second.UnmatchedLeft.Select(l => l.Id));
        Assert.Equal(first.UnmatchedRight.Select(r => r.Id), second.UnmatchedRight.Select(r => r.Id));
    }

    private static (FuzzLeft[] Left, FuzzRight[] Right) BuildDataset(int seed)
    {
        var random = new Random(seed);
        var leftCount = random.Next(MinRecords, MaxRecords + 1);
        var rightCount = random.Next(MinRecords, MaxRecords + 1);

        var left = new List<FuzzLeft>(leftCount);
        for (var i = 0; i < leftCount; i++)
        {
            left.Add(new FuzzLeft(
                left.Count,
                Pool(random, "REF", RefPoolSize),
                Pool(random, "ALT", AltRefPoolSize),
                Pool(random, "PAY", PayoutPoolSize),
                Amount(random),
                Date(random)));
        }

        var right = new List<FuzzRight>(rightCount);
        for (var i = 0; i < rightCount; i++)
        {
            right.Add(new FuzzRight(
                right.Count,
                Pool(random, "REF", RefPoolSize),
                Pool(random, "ALT", AltRefPoolSize),
                Pool(random, "PAY", PayoutPoolSize),
                Amount(random),
                Date(random)));
        }

        PlantSettlementGroup(seed, random, left, right);
        return (left.ToArray(), right.ToArray());
    }

    // Plants one genuine gross-vs-net settlement per dataset: a handful of same-payout left invoices
    // whose amounts sum to a single net payout on the right. Their reference keys are null so no earlier
    // pass can steal them, leaving the group pass to claim the whole set. This guarantees the multi-left
    // group path is exercised, while conservation must still hold across the planted and random records.
    private static void PlantSettlementGroup(
        int seed, Random random, List<FuzzLeft> left, List<FuzzRight> right)
    {
        var payout = $"PLANT-{seed}";
        var members = random.Next(2, 4);
        var total = 0m;
        for (var i = 0; i < members; i++)
        {
            var amount = random.Next(MinKobo, MaxKoboExclusive) / KoboPerUnit;
            total += amount;
            left.Add(new FuzzLeft(left.Count, Reference: null, AltRef: null, payout, amount, Date(random)));
        }

        right.Add(new FuzzRight(right.Count, Reference: null, AltRef: null, payout, total, Date(random)));
    }

    private static string? Pool(Random random, string prefix, int size)
        => random.Next(NullInNParts) == 0 ? null : $"{prefix}-{random.Next(size)}";

    private static decimal Amount(Random random)
        => random.Next(ZeroInNAmounts) == 0
            ? 0m
            : random.Next(MinKobo, MaxKoboExclusive) / KoboPerUnit;

    private static DateTime Date(Random random) => BaseDate.AddDays(random.Next(MaxAgeDays));

    private static (FuzzLeft[] Left, FuzzRight[] Right) Shuffle(FuzzLeft[] left, FuzzRight[] right, int seed)
    {
        var random = new Random(seed * 2 + 1);
        var shuffledLeft = (FuzzLeft[])left.Clone();
        var shuffledRight = (FuzzRight[])right.Clone();
        FisherYates(shuffledLeft, random);
        FisherYates(shuffledRight, random);
        return (shuffledLeft, shuffledRight);
    }

    private static void FisherYates<T>(T[] items, Random random)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
