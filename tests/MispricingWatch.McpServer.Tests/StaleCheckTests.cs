using Microsoft.Extensions.Logging.Abstractions;
using MispricingWatch.CurveLab;

namespace MispricingWatch.McpServer.Tests;

public class StaleCheckTests
{
    private static readonly StaleParams Defaults = new();

    // A published series that moves every tick, then holds one value for the last flatTicks ticks.
    private static double[] MovingThenFlat(int total, int flatTicks)
    {
        var series = new double[total];
        for (var t = 0; t < total; t++)
        {
            series[t] = 3.0 + 0.001 * t;
        }
        for (var t = total - flatTicks; t < total; t++)
        {
            series[t] = series[total - flatTicks - 1];
        }
        return series;
    }

    private static double[] Moving(int total)
    {
        var series = new double[total];
        for (var t = 0; t < total; t++)
        {
            series[t] = 3.0 + 0.001 * t;
        }
        return series;
    }

    private static double[] Still(int total)
    {
        var series = new double[total];
        for (var t = 0; t < total; t++)
        {
            series[t] = 3.0;
        }
        return series;
    }

    [Test]
    public void Compute_HealthyWorld_NoUnchangedTicksAndNotStale()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var (prices, _) = WorldBuilder.Build(seed, plantFault: false);
            foreach (var node in prices)
            {
                var result = StaleCheck.Compute(node, Defaults);

                Assert.That(result.UnchangedTicks, Is.EqualTo(0));
                Assert.That(result.Stale, Is.False);
            }
        }
    }

    [Test]
    public void Compute_PlantedStalePrice_ReportsRunLengthSinceOnset()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var (prices, truth) = WorldBuilder.Build(seed, true, FaultType.StalePrice);
            var fault = truth.Fault!;
            var node = prices.Single(p => p.Tenor == fault.Tenor);

            var result = StaleCheck.Compute(node, Defaults);

            Assert.That(result.UnchangedTicks, Is.EqualTo(WorldBuilder.TotalTicks - fault.OnsetTick));
            Assert.That(result.Stale, Is.True);
            Assert.That(result.BenchmarkMoveBps, Is.GreaterThan(0));
        }
    }

    [Test]
    public void Compute_FlatPriceButStillBenchmark_IsAQuietMarketNotStale()
    {
        var node = new NodePrices("10Y", MovingThenFlat(500, 100), Still(500));

        var result = StaleCheck.Compute(node, Defaults);

        Assert.That(result.UnchangedTicks, Is.EqualTo(100));
        Assert.That(result.BenchmarkMoveBps, Is.EqualTo(0));
        Assert.That(result.Stale, Is.False);
    }

    [Test]
    public void Compute_FlatPriceWhileBenchmarkMoves_IsStale()
    {
        var node = new NodePrices("10Y", MovingThenFlat(500, 100), Moving(500));

        var result = StaleCheck.Compute(node, Defaults);

        Assert.That(result.UnchangedTicks, Is.EqualTo(100));
        Assert.That(result.BenchmarkMoveBps, Is.GreaterThan(0));
        Assert.That(result.Stale, Is.True);
    }

    [Test]
    public void Compute_FlatRunShorterThanMinimum_NotStale()
    {
        var node = new NodePrices("10Y", MovingThenFlat(500, 10), Moving(500));

        var result = StaleCheck.Compute(node, Defaults);

        Assert.That(result.UnchangedTicks, Is.EqualTo(10));
        Assert.That(result.Stale, Is.False);
    }

    [Test]
    public void Compute_NoBenchmark_LongFlatRunAloneIsStale()
    {
        var node = new NodePrices("3W", MovingThenFlat(500, 100), null);

        var result = StaleCheck.Compute(node, Defaults);

        Assert.That(result.HasBenchmark, Is.False);
        Assert.That(result.BenchmarkMoveBps, Is.Null);
        Assert.That(result.Stale, Is.True);
    }

    [Test]
    public void CheckStale_UnknownTenor_ReturnsErrorNotException()
    {
        var tools = new PricingTools(new WorldService(NullLogger<WorldService>.Instance));

        var result = tools.CheckStale("99Y");

        Assert.That(result.Error, Does.Contain("unknown tenor"));
        Assert.That(result.Stale, Is.False);
    }
}
