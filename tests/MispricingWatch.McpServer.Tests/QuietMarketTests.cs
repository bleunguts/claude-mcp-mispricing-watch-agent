using MispricingWatch.CurveLab;

namespace MispricingWatch.McpServer.Tests;

// A quiet world plants no fault, so any alarm on any node is a false alarm. This test never reads GroundTruth.
public class QuietMarketTests
{
    private const int Seeds = 100;

    [Test]
    public void QuietWorld_SpreadOrStale_NeverAlarms()
    {
        var spreadParams = new SpreadParams();
        var staleParams = new StaleParams();
        var falseAlarms = new List<string>();

        for (var seed = 1; seed <= Seeds; seed++)
        {
            var (prices, _) = WorldBuilder.Build(seed, plantFault: false, quietMarket: true);
            foreach (var node in prices)
            {
                var spread = SpreadStats.Compute(node, spreadParams).OutsideTolerance;
                var stale = StaleCheck.Compute(node, staleParams).Stale;
                if (spread || stale)
                {
                    falseAlarms.Add($"seed {seed} {node.Tenor}: spread {spread}, stale {stale}");
                }
            }
        }

        Console.WriteLine($"Quiet world, {Seeds} seeds: {falseAlarms.Count} false alarm(s)");
        falseAlarms.ForEach(Console.WriteLine);
        Assert.That(falseAlarms, Is.Empty);
    }
}
