namespace MispricingWatch.CurveLab;

/// <summary>The answer key. Only the scorer may see this; tools and agent never get it.</summary>
public sealed record GroundTruth(MarketMoveLabel MarketMove, FaultLabel? Fault);

public static class WorldBuilder
{
    public const int TicksPerMonth = 12_600;     // 21 days x 600 one-minute ticks
    public const double FaultOffsetBps = 5.0;

    /// <summary>
    /// Builds the whole world from one seed: market, one genuine market move, published and
    /// benchmark series, and (optionally) one setting-change fault on a benchmarked node.
    /// Prices go to the tools. Truth goes to the scorer.
    /// </summary>
    public static (NodePrices[] Prices, GroundTruth Truth) Build(int seed, bool plantFault = true)
    {
        var nodes = NodeSpec.Mvp;
        var trueRates = MarketSimulator.GenerateTrueRates(nodes, TicksPerMonth, seed);
        var move = new ScenarioInjector().ApplyMarketMove(trueRates, seed);
        var prices = PricingSimulator.Generate(nodes, trueRates, seed);

        FaultLabel? fault = null;
        if (plantFault)
        {
            var rng = new Random(seed ^ 0xFA17);
            var benchmarked = prices.Where(p => p.Benchmark is not null).ToArray();
            var victim = benchmarked[rng.Next(benchmarked.Length)];
            var onset = rng.Next(TicksPerMonth / 2, TicksPerMonth * 9 / 10); // history before and after
            fault = FaultInjector.ApplySettingChange(victim, onset, int.MaxValue, FaultOffsetBps);
        }

        return (prices, new GroundTruth(move, fault));
    }
}
