using System.Diagnostics;

namespace MispricingWatch.CurveLab.Tests;

public class MarketSimulatorTests
{
    private const int Seed = 42;
    private const int MonthOfTicks = 12_600; // 21 days x 600 one-minute ticks

    private static double[] DiffsBps(double[] rates) =>
        rates.Skip(1).Zip(rates, (cur, prev) => (cur - prev) * 100.0).ToArray();

    private static double StdDev(double[] x)
    {
        var mean = x.Average();
        return Math.Sqrt(x.Sum(v => (v - mean) * (v - mean)) / (x.Length - 1));
    }

    private static double Correlation(double[] a, double[] b)
    {
        var ma = a.Average();
        var mb = b.Average();
        var cov = a.Zip(b, (x, y) => (x - ma) * (y - mb)).Sum();
        return cov / Math.Sqrt(a.Sum(x => (x - ma) * (x - ma)) * b.Sum(y => (y - mb) * (y - mb)));
    }

    [Test]
    public void GenerateTrueRates_SameSeed_IdenticalOutput()
    {
        var a = MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, 1000, Seed);
        var b = MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, 1000, Seed);

        for (var n = 0; n < a.Length; n++)
            Assert.That(b[n], Is.EqualTo(a[n]));
    }

    [Test]
    public void GenerateTrueRates_DifferentSeed_DifferentOutput()
    {
        var a = MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, 1000, Seed);
        var b = MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, 1000, Seed + 1);

        Assert.That(b[0], Is.Not.EqualTo(a[0]));
    }

    [Test]
    public void GenerateTrueRates_StartsAtConfiguredRate()
    {
        var rates = MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, 10, Seed);

        for (var n = 0; n < NodeSpec.Mvp.Count; n++)
            Assert.That(rates[n][0], Is.EqualTo(NodeSpec.Mvp[n].StartRatePct));
    }

    [Test]
    public void GenerateTrueRates_RealisedVolatility_WithinTenPercentOfConfigured()
    {
        var rates = MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, MonthOfTicks, Seed);

        for (var n = 0; n < NodeSpec.Mvp.Count; n++)
            Assert.That(StdDev(DiffsBps(rates[n])),
                Is.EqualTo(NodeSpec.Mvp[n].VolBpsPerTick).Within(10).Percent,
                NodeSpec.Mvp[n].Tenor);
    }

    [Test]
    public void GenerateTrueRates_AdjacentTenors_PositivelyCorrelated()
    {
        var rates = MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, MonthOfTicks, Seed);

        for (var n = 0; n < NodeSpec.Mvp.Count - 1; n++)
            Assert.That(Correlation(DiffsBps(rates[n]), DiffsBps(rates[n + 1])), Is.GreaterThan(0.6),
                $"{NodeSpec.Mvp[n].Tenor} vs {NodeSpec.Mvp[n + 1].Tenor}");
    }

    [Test]
    public void GenerateTrueRates_MonthOfHistory_UnderOneSecond()
    {
        var sw = Stopwatch.StartNew();
        MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, MonthOfTicks, Seed);

        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
    }
}
