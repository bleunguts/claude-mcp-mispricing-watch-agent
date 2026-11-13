namespace MispricingWatch.CurveLab;

// WHAT THIS FILE DOES, IN PLAIN WORDS
//
// It invents the "true" market: a rate for every node (2Y, 5Y, ...) for every minute
// of a month. Nobody outside the simulator ever sees these true rates. Later we will
// build the PUBLISHED price and the BENCHMARK from them, then break the published
// price on purpose so we know the right answer when we test the agent.
//
// How a true rate is invented: start at a starting rate. Every minute, nudge it up or
// down by a small random amount. Do that thousands of times and you get a wiggly line
// that looks like a real rate. That is all a "random walk" is: a drunk person taking
// tiny random steps along a line.
//
// Two ingredients make it believable:
//   1. Steps are usually tiny and only occasionally big (see NextGaussian below).
//   2. The nodes move partly TOGETHER, like boats on the same sea (see "shared wave").

/// <summary>
/// Generates the hidden TRUE rate per node per tick as a correlated random walk.
/// Each tick: one shared shock for the whole curve plus a small own shock per node.
/// </summary>
public static class MarketSimulator
{
    /// <summary>
    /// How much of each node's nudge comes from the shared wave rather than its own luck.
    /// 0.9 means "mostly follows the herd", so neighbouring nodes move together
    /// about 80% as strongly as a perfect lockstep (a correlation of about 0.81).
    /// </summary>
    public const double CommonFactorWeight = 0.9;

    /// <returns>true rates in percent, indexed [node][tick]; tick 0 is the start rate.</returns>
    public static double[][] GenerateTrueRates(IReadOnlyList<NodeSpec> nodes, int ticks, int seed)
    {
        // A repeatable dice-roller. Same seed in, same "random" rolls out, every run.
        // That is what lets us replay the exact same fake world when debugging.
        var rng = new Random(seed);

        // How much of each nudge is the node's own luck. This is chosen so that shared
        // wave + own luck together are still a normal-sized nudge (not bigger or smaller).
        var own = Math.Sqrt(1 - CommonFactorWeight * CommonFactorWeight);

        // One row per node, one slot per minute. Every node begins at its starting rate.
        var rates = nodes.Select(_ => new double[ticks]).ToArray();
        for (var n = 0; n < nodes.Count; n++)
            rates[n][0] = nodes[n].StartRatePct;

        // Walk forward one minute at a time.
        for (var t = 1; t < ticks; t++)
        {
            // The shared wave for this minute: ONE random number used by every node.
            // If it is positive, the whole curve is pushed up; if negative, down.
            var sharedWave = NextGaussian(rng);

            for (var n = 0; n < nodes.Count; n++)
            {
                // This node's nudge = mostly the shared wave + a bit of its own luck.
                var nudge = CommonFactorWeight * sharedWave + own * NextGaussian(rng);

                // Scale the nudge to how jumpy this node is (e.g. 0.3 basis points).
                // A basis point is one hundredth of a percent.
                var moveBps = nodes[n].VolBpsPerTick * nudge;

                // New rate = last minute's rate + this minute's nudge.
                // The "/ 100" converts basis points to percent, the unit rates are stored in.
                rates[n][t] = rates[n][t - 1] + moveBps / 100.0;
            }
        }
        return rates;
    }

    // WHY THIS METHOD EXISTS
    //
    // The dice-roller (rng.NextDouble) gives a FLAT random number between 0 and 1: every
    // value is equally likely. Real market nudges are not like that. Most are tiny, some
    // are medium, very big ones are rare. Plot how often each size happens and you get a
    // bell shape centred on zero. That bell is called the "normal" (or Gaussian) shape.
    //
    // .NET has no built-in way to roll a bell-shaped number, so we build one from flat
    // rolls. The trick: roll 12 dice and add them up. One die is flat, but the TOTAL of
    // many dice clusters in the middle and rarely lands near the extremes: a bell shape.
    // Subtracting 6 (the average total) centres it on zero, and the typical size comes
    // out at about 1. Multiplying by "how jumpy" then makes it the size we want.
    //
    // NOTE: this is a deliberate MVP simplification. In reality we would use a proper
    // normal distribution generator such as Box-Muller (what option-pricing Monte Carlo
    // uses): it needs a logarithm and a cosine, but has no cap. The dice trick can never
    // go beyond +/-6, so it never produces the very rarest huge moves. That does not
    // matter for this simulator, and it is far easier to explain. Swap it in later if needed.
    private static double NextGaussian(Random rng)
    {
        var total = 0.0;
        for (var i = 0; i < 12; i++)
            total += rng.NextDouble();
        return total - 6.0;
    }
}
