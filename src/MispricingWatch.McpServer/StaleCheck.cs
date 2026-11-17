using MispricingWatch.CurveLab;

namespace MispricingWatch.McpServer;

/// <summary>Detector settings. They come from server config, never from the caller.</summary>
public sealed record StaleParams(int MinUnchangedTicks = 30)
{
    public static StaleParams FromEnvironment()
    {
        int minUnchangedTicks;
        if (int.TryParse(Environment.GetEnvironmentVariable("STALE_MIN_TICKS"), out minUnchangedTicks))
        {
            return new StaleParams(minUnchangedTicks);
        }
        return new StaleParams();
    }
}

/// <summary>
/// Evidence about whether one node's published price has frozen.
/// UnchangedTicks: how many of the latest ticks in a row are identical to the tick before them.
/// BenchmarkMoveBps: how far the benchmark moved over those same ticks (highest minus lowest, in bps),
/// or null when the node has no benchmark.
/// </summary>
public sealed record StaleCheckResult(
    string? Tenor, int UnchangedTicks, bool HasBenchmark, double? BenchmarkMoveBps, bool Stale, string? Error);

public static class StaleCheck
{
    public static StaleCheckResult Compute(NodePrices node, StaleParams settings)
    {
        double[] published = node.Published;
        int lastTick = published.Length - 1;

        // Step 1: count back from the latest tick while the price has not changed from the tick before it.
        int unchangedTicks = 0;
        int tick = lastTick;
        while (tick >= 1 && published[tick] == published[tick - 1])
        {
            unchangedTicks++;
            tick--;
        }

        // Step 2: over those same ticks, how far did the benchmark move? It starts at the last good tick,
        // the one just before the price froze.
        bool hasBenchmark = node.Benchmark != null;
        double? benchmarkMoveBps = null;
        if (hasBenchmark)
        {
            double lowest = node.Benchmark![lastTick];
            double highest = node.Benchmark[lastTick];
            for (int i = lastTick - unchangedTicks; i <= lastTick; i++)
            {
                if (node.Benchmark[i] < lowest)
                {
                    lowest = node.Benchmark[i];
                }
                if (node.Benchmark[i] > highest)
                {
                    highest = node.Benchmark[i];
                }
            }
            benchmarkMoveBps = Math.Round((highest - lowest) * 100.0, 3);
        }

        // Step 3: stale means flat for long enough while the market was moving. A flat price next to a flat
        // benchmark is just a quiet market. With no benchmark there is nothing to compare, so flat alone decides.
        bool flatLongEnough = unchangedTicks >= settings.MinUnchangedTicks;
        bool marketMoved = true;
        if (hasBenchmark)
        {
            marketMoved = benchmarkMoveBps > 0;
        }
        bool stale = flatLongEnough && marketMoved;

        return new StaleCheckResult(node.Tenor, unchangedTicks, hasBenchmark, benchmarkMoveBps, stale, null);
    }
}
