using System.Diagnostics;
using Xunit.Abstractions;

namespace Reconcile.Net.Tests.Performance;

public sealed record PerfLeft(string Reference, decimal Amount);

public sealed record PerfRight(string Reference, decimal Amount);

public class LinearityTests
{
    private const int WarmupSize = 10_000;
    private const int BaseSize = 100_000;
    private const int DoubledSize = 200_000;
    private const int TimedRunsPerSize = 3;
    private const double MaxLinearScalingRatio = 3.5;
    private const int MaxBaseRunMilliseconds = 10_000;
    private const int ShuffleSeed = 20_260_803;
    private const decimal AmountStep = 0.01m;

    private readonly ITestOutputHelper _output;

    public LinearityTests(ITestOutputHelper output) => _output = output;

    private static (PerfLeft[] Left, PerfRight[] Right) BuildStreams(int size)
    {
        var left = new PerfLeft[size];
        for (var i = 0; i < size; i++)
        {
            left[i] = new PerfLeft($"REF-{i}", i * AmountStep);
        }

        var shuffled = Enumerable.Range(0, size).ToArray();
        var random = new Random(ShuffleSeed);
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var right = new PerfRight[size];
        for (var i = 0; i < size; i++)
        {
            var source = shuffled[i];
            right[i] = new PerfRight($"REF-{source}", source * AmountStep);
        }

        return (left, right);
    }

    private static ReconciliationResult<PerfLeft, PerfRight> RunKeyedPass(
        PerfLeft[] left, PerfRight[] right)
        => Reconciliation
            .Between(left, right)
            .ThenMatchOn(pass => pass
                .Key(l => l.Reference, r => r.Reference)
                .Amount(l => l.Amount, r => r.Amount))
            .Run();

    private long BestRunMilliseconds(int size)
    {
        var (left, right) = BuildStreams(size);
        var best = long.MaxValue;
        for (var run = 0; run < TimedRunsPerSize; run++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = RunKeyedPass(left, right);
            stopwatch.Stop();
            Assert.Equal(size, result.Matched.Count);
            Assert.Empty(result.UnmatchedLeft);
            Assert.Empty(result.UnmatchedRight);
            best = Math.Min(best, stopwatch.ElapsedMilliseconds);
        }

        _output.WriteLine($"{size:N0} x {size:N0} keyed pass: best of {TimedRunsPerSize} runs = {best} ms");
        return best;
    }

    [Fact]
    public void Hundred_thousand_by_hundred_thousand_keyed_pass_matches_everything()
    {
        var (left, right) = BuildStreams(BaseSize);
        var stopwatch = Stopwatch.StartNew();
        var result = RunKeyedPass(left, right);
        stopwatch.Stop();

        Assert.Equal(BaseSize, result.Matched.Count);
        Assert.Empty(result.UnmatchedLeft);
        Assert.Empty(result.UnmatchedRight);
        Assert.True(
            stopwatch.ElapsedMilliseconds < MaxBaseRunMilliseconds,
            $"100k x 100k keyed pass took {stopwatch.ElapsedMilliseconds} ms, expected under {MaxBaseRunMilliseconds} ms.");
        _output.WriteLine($"100k x 100k single-shot keyed pass: {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Keyed_pass_scales_linearly_not_quadratically()
    {
        BestRunMilliseconds(WarmupSize);

        var baseMs = BestRunMilliseconds(BaseSize);
        var doubledMs = BestRunMilliseconds(DoubledSize);

        var ratio = (double)doubledMs / Math.Max(baseMs, 1);
        _output.WriteLine($"Scaling ratio for 2x input: {ratio:F2} (linear ~2.0, quadratic ~4.0)");
        Assert.True(
            ratio <= MaxLinearScalingRatio,
            $"Doubling the input scaled time by {ratio:F2}x " +
            $"({baseMs} ms -> {doubledMs} ms); expected at most {MaxLinearScalingRatio}x for O(n+m).");
    }
}
