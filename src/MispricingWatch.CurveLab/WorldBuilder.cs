namespace MispricingWatch.CurveLab;

/// <summary>The answer key. Only the scorer may see this; tools and agent never get it.</summary>
public sealed record GroundTruth(MarketMoveLabel MarketMove, FaultLabel? Fault, QuietMarketLabel? Quiet = null);

public static class WorldBuilder
{
    public const int TicksPerDay = 240;                  // 4 hours x 60 one-minute ticks
    public const int Days = 14;
    public const int TotalTicks = TicksPerDay * Days;     // 3,360 per node
    public const double FaultOffsetBps = 5.0;

    /// <summary>
    /// Builds the whole world from one seed: market, one genuine market move, published and
    /// benchmark series, and (optionally) one planted fault or one quiet spell on a benchmarked node.
    /// Prices go to the tools. Truth goes to the scorer.
    /// </summary>
    public static (NodePrices[] Prices, GroundTruth Truth) Build(
        int seed, bool plantFault = true, FaultType faultType = FaultType.SettingChange, bool quietMarket = false)
    {
        if (plantFault && quietMarket)
        {
            throw new ArgumentException("A world has one planted thing: a fault or a quiet market, not both.", nameof(quietMarket));
        }

        var nodes = NodeSpec.Mvp;
        var trueRates = MarketSimulator.GenerateTrueRates(nodes, TotalTicks, seed);
        var move = new ScenarioInjector().ApplyMarketMove(trueRates, seed);
        var prices = PricingSimulator.Generate(nodes, trueRates, seed);

        FaultLabel? fault = null;
        if (plantFault)
        {
            var rng = new Random(seed ^ 0xFA17);
            var benchmarked = prices.Where(p => p.Benchmark is not null).ToArray();
            var victim = benchmarked[rng.Next(benchmarked.Length)];
            var onset = rng.Next(TotalTicks / 2, TotalTicks * 9 / 10); // history before and after
            fault = faultType == FaultType.StalePrice
                ? FaultInjector.ApplyStalePrice(victim, onset, durationTicks: null)
                : FaultInjector.ApplySettingChange(victim, onset, durationTicks: null, FaultOffsetBps);
        }

        QuietMarketLabel? quiet = null;
        if (quietMarket)
        {
            var rng = new Random(seed ^ 0x0A1E7);
            var benchmarked = prices.Where(p => p.Benchmark is not null).ToArray();
            var victim = benchmarked[rng.Next(benchmarked.Length)];
            var onset = rng.Next(TotalTicks / 2, TotalTicks * 9 / 10);
            quiet = QuietMarketInjector.Apply(victim, onset);
        }

        return (prices, new GroundTruth(move, fault, quiet));
    }
}
