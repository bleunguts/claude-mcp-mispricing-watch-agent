using MispricingWatch.CurveLab;

namespace MispricingWatch.McpServer.Tests;

public class SpreadStatsTests
{
    private static readonly SpreadParams Defaults = new();
    private static readonly int[] Seeds = Enumerable.Range(1, 20).ToArray();

    [Test]
    public void Compute_FaultFreeWorld_NoLongRunOutsideTolerance()
    {
        foreach (var seed in Seeds)
        {
            var (prices, _) = WorldBuilder.Build(seed, plantFault: false);
            foreach (var node in prices.Where(n => n.Benchmark is not null))
            {
                var result = SpreadStats.Compute(node, Defaults);
                Assert.That(result.TicksOutside, Is.LessThan(5), $"seed {seed}, {node.Tenor}");
            }
        }
    }

    [Test]
    public void Compute_PlantedFaultOnGovernmentNode_LargeScoreAndPersistenceSinceOnset()
    {
        var tested = 0;
        foreach (var seed in Seeds)
        {
            var (prices, truth) = WorldBuilder.Build(seed);
            var fault = truth.Fault!;
            if (fault.Tenor == "30Y") continue;   // competitor benchmark is noisier; covered separately

            var result = SpreadStats.Compute(prices.Single(n => n.Tenor == fault.Tenor), Defaults);

            Assert.That(result.Score, Is.GreaterThan(10), $"seed {seed}");
            Assert.That(result.TicksOutside, Is.GreaterThanOrEqualTo(WorldBuilder.TotalTicks - fault.OnsetTick), $"seed {seed}");
            tested++;
        }
        Assert.That(tested, Is.GreaterThan(0));
    }

    [Test]
    public void Compute_PlantedFaultOnCompetitorNode_ScoreIsPositiveAndAboveGovernmentTolerance()
    {
        var tested = 0;
        foreach (var seed in Enumerable.Range(1, 60))
        {
            var (prices, truth) = WorldBuilder.Build(seed);
            if (truth.Fault!.Tenor != "30Y") continue;

            var result = SpreadStats.Compute(prices.Single(n => n.Tenor == "30Y"), Defaults);

            Assert.That(result.Score, Is.GreaterThan(2), $"seed {seed}");
            tested++;
        }
        Assert.That(tested, Is.GreaterThan(0));
    }

    [Test]
    public void Compute_NodeWithNoBenchmark_IsNotAvailable()
    {
        var (prices, _) = WorldBuilder.Build(1);

        var result = SpreadStats.Compute(prices.Single(n => n.Tenor == "3W"), Defaults);

        Assert.That(result.Available, Is.False);
        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public void Compute_FloorLargerThanKSigma_ToleranceIsTheFloor()
    {
        var (prices, _) = WorldBuilder.Build(1, plantFault: false);

        var result = SpreadStats.Compute(prices.Single(n => n.Tenor == "10Y"), new SpreadParams(K: 4, FloorBps: 50));

        Assert.That(result.ToleranceBps, Is.EqualTo(50));
    }

    [Test]
    public void Compute_SameSeed_IdenticalResult()
    {
        var a = SpreadStats.Compute(WorldBuilder.Build(7).Prices[2], Defaults);
        var b = SpreadStats.Compute(WorldBuilder.Build(7).Prices[2], Defaults);

        Assert.That(a, Is.EqualTo(b));
    }
}
