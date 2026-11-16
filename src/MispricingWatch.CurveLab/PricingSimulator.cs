namespace MispricingWatch.CurveLab;

// WHAT THIS FILE DOES, IN PLAIN WORDS
//
// MarketSimulator invented the hidden TRUE rate. This file builds the only two things
// the agent will ever be allowed to see:
//
//   PUBLISHED  = what our bank puts out: the truth plus a tiny wobble of its own.
//   BENCHMARK  = an outside opinion of the same rate: the truth plus ITS own wobble.
//
// Think of two thermometers on a wall, both measuring the same real temperature. When
// the weather warms up, both climb together, so the gap between them stays small. If
// one breaks, the gap opens. That gap (published minus benchmark) is what the agent
// will watch, because real market moves cancel out of it.
//
// Benchmarks come in three qualities, because in real life they are not equally good:
//   Government : small wobble, no delay    -> strong evidence
//   Competitor : bigger wobble, arrives late -> weak evidence
//   None       : no outside opinion exists -> reported as null, never an empty list
//
// No faults are planted here yet. This is the healthy world.

/// <summary>What the outside world may see for one node. The hidden true rate is NOT in here.</summary>
/// <param name="Benchmark">null means "no benchmark exists for this node".</param>
public sealed record NodePrices(string Tenor, double[] Published, double[]? Benchmark);

public static class PricingSimulator
{
    // Wobble sizes in basis points (one hundredth of a percent). Placeholders, tune later.
    public const double PublishedNoiseBps = 0.1;
    public const double GovernmentNoiseBps = 0.2;
    public const double CompetitorNoiseBps = 0.8;

    /// <summary>The competitor benchmark shows what the truth was this many ticks ago.</summary>
    public const int CompetitorLagTicks = 3;

    /// <param name="trueRates">from MarketSimulator, indexed [node][tick], in percent.</param>
    public static NodePrices[] Generate(IReadOnlyList<NodeSpec> nodes, double[][] trueRates, int seed)
    {
        // Its own dice-roller, so adding this file did not change the true rates for a seed.
        // Same seed in, same world out.
        var rng = new Random(seed ^ 0x5EED);

        var result = new NodePrices[nodes.Count];
        for (var n = 0; n < nodes.Count; n++)
        {
            var truth = trueRates[n];
            var published = new double[truth.Length];
            var benchmark = nodes[n].Benchmark == BenchmarkKind.None ? null : new double[truth.Length];

            for (var t = 0; t < truth.Length; t++)
            {
                published[t] = truth[t] + Wobble(rng, PublishedNoiseBps);

                if (benchmark is null) continue;
                var (noiseBps, lag) = nodes[n].Benchmark == BenchmarkKind.Government
                    ? (GovernmentNoiseBps, 0)
                    : (CompetitorNoiseBps, CompetitorLagTicks);

                // A late benchmark reports what the truth was a few ticks ago
                // (the very first ticks just reuse tick 0).
                benchmark[t] = truth[Math.Max(0, t - lag)] + Wobble(rng, noiseBps);
            }

            result[n] = new NodePrices(nodes[n].Tenor, published, benchmark);
        }
        return result;
    }

    // A random wobble of the given size in basis points, returned in percent.
    private static double Wobble(Random rng, double sizeBps) =>
        MarketSimulator.NextGaussian(rng) * sizeBps / 100.0;
}
