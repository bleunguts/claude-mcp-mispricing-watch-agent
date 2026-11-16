namespace MispricingWatch.CurveLab.Tests;
public class PricingSimulatorTests
{
    private const int Seed = 42;
    private const int MonthOfTicks = 12_600;

    // Expected spread (published - benchmark) size in bps for each kind, from the noise sizes.
    // Government: sqrt(0.1^2 + 0.2^2). Competitor (30Y, vol 0.2): adds the lag effect, 3 ticks of walk.
    private static readonly double GovernmentSpreadBps = Math.Sqrt(0.1 * 0.1 + 0.2 * 0.2);
    private static readonly double CompetitorSpreadBps = Math.Sqrt(0.1 * 0.1 + 0.8 * 0.8 + 3 * 0.2 * 0.2);

    private static NodePrices[] Build(int seed = Seed, int ticks = MonthOfTicks)
    {
        var truth = MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, ticks, seed);
        return PricingSimulator.Generate(NodeSpec.Mvp, truth, seed);
    }

    private static NodePrices Node(NodePrices[] all, string tenor) => all.Single(p => p.Tenor == tenor);    

    private static double StdDev(double[] x)
    {
        var mean = x.Average();
        return Math.Sqrt(x.Sum(v => (v - mean) * (v - mean)) / (x.Length - 1));
    }

    [Test]
    public void Generate_SameSeed_IdenticalOutput()
    {
        var a = Build();
        var b = Build();

        for (var n = 0; n < a.Length; n++)
        {
            Assert.That(b[n].Published, Is.EqualTo(a[n].Published));
            Assert.That(b[n].Benchmark, Is.EqualTo(a[n].Benchmark));
        }
    }

    [Test]
    public void Generate_NodeWithNoBenchmark_ReportsNullNotEmpty()
    {
        var prices = Node(Build(), "3W");

        Assert.That(prices.Benchmark, Is.Null);
        Assert.That(prices.Published, Has.Length.EqualTo(MonthOfTicks));
    }

    [Test]
    public void Generate_NodesWithBenchmark_HaveFullLengthSeries()
    {
        foreach (var p in Build().Where(p => p.Tenor != "3W"))
            Assert.That(p.Benchmark, Has.Length.EqualTo(MonthOfTicks), p.Tenor);
    }

    [Test]
    public void Generate_GovernmentSpread_CentredOnZeroWithConfiguredSize()
    {
        var spread = Utility.SpreadBps(Node(Build(), "5Y"));

        Assert.That(spread.Average(), Is.EqualTo(0.0).Within(0.02));
        Assert.That(StdDev(spread), Is.EqualTo(GovernmentSpreadBps).Within(10).Percent);
    }

    [Test]
    public void Generate_CompetitorSpread_CentredOnZeroWithConfiguredSize()
    {
        var spread = Utility.SpreadBps(Node(Build(), "30Y"));

        Assert.That(spread.Average(), Is.EqualTo(0.0).Within(0.1));
        Assert.That(StdDev(spread), Is.EqualTo(CompetitorSpreadBps).Within(10).Percent);
    }

    [Test]
    public void Generate_GovernmentSpread_TighterThanCompetitor()
    {
        var all = Build();

        Assert.That(StdDev(Utility.SpreadBps(Node(all, "5Y"))), Is.LessThan(StdDev(Utility.SpreadBps(Node(all, "30Y")))));
    }

    [Test]
    public void Generate_GovernmentSpread_StationaryFirstHalfVsSecondHalf()
    {
        var spread = Utility.SpreadBps(Node(Build(), "10Y"));
        var half = spread.Length / 2;

        var first = StdDev(spread[..half]);
        var second = StdDev(spread[half..]);

        Assert.That(second, Is.EqualTo(first).Within(10).Percent);
    }

    [Test]
    public void Generate_MarketMove_CancelsOutOfSpread()
    {
        // The true rate wanders a lot over a month; the spread must not wander with it.
        var truth = MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, MonthOfTicks, Seed);
        var prices = PricingSimulator.Generate(NodeSpec.Mvp, truth, Seed);
        var i = NodeSpec.Mvp.ToList().FindIndex(s => s.Tenor == "5Y");

        var truthRangeBps = (truth[i].Max() - truth[i].Min()) * 100.0;
        var spread = Utility.SpreadBps(prices[i]);
        var spreadRangeBps = spread.Max() - spread.Min();

        Assert.That(spreadRangeBps, Is.LessThan(truthRangeBps / 5));
    }
}
