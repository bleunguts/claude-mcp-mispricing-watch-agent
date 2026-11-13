namespace MispricingWatch.CurveLab;

/// <summary>
/// Generates the hidden TRUE rate per node per tick as a correlated random walk.
/// Each tick: one shared shock for the whole curve plus a small own shock per node.
/// </summary>
public static class MarketSimulator
{
    /// <summary>Weight on the common factor. 0.9 gives adjacent nodes a correlation of about 0.81.</summary>
    public const double CommonFactorWeight = 0.9;

    /// <returns>true rates in percent, indexed [node][tick]; tick 0 is the start rate.</returns>
    public static double[][] GenerateTrueRates(IReadOnlyList<NodeSpec> nodes, int ticks, int seed)
    {
        var rng = new Random(seed);
        var own = Math.Sqrt(1 - CommonFactorWeight * CommonFactorWeight);

        var rates = nodes.Select(_ => new double[ticks]).ToArray();
        for (var n = 0; n < nodes.Count; n++)
            rates[n][0] = nodes[n].StartRatePct;

        for (var t = 1; t < ticks; t++)
        {
            var common = NextGaussian(rng);
            for (var n = 0; n < nodes.Count; n++)
            {
                var shock = CommonFactorWeight * common + own * NextGaussian(rng);
                var moveBps = nodes[n].VolBpsPerTick * shock;
                rates[n][t] = rates[n][t - 1] + moveBps / 100.0; // bps -> percent
            }
        }
        return rates;
    }

    // Box-Muller: turns two uniform draws into one standard normal draw.
    private static double NextGaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble(); // avoid log(0)
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
