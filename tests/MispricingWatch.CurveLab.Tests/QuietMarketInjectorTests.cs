namespace MispricingWatch.CurveLab.Tests;

public class QuietMarketInjectorTests
{
    private static NodePrices Node(bool withBenchmark = true)
    {
        var published = Enumerable.Range(0, 100).Select(i => 3.0 + i * 0.001).ToArray();
        var benchmark = withBenchmark
            ? Enumerable.Range(0, 100).Select(i => 3.0 + i * 0.001 + 0.0002).ToArray()
            : null;
        return new NodePrices("10Y", published, benchmark);
    }

    [Test]
    public void Apply_FromOnset_BothSeriesHoldTheirLastGoodValue()
    {
        var node = Node();
        var lastGoodPublished = node.Published[59];
        var lastGoodBenchmark = node.Benchmark![59];

        QuietMarketInjector.Apply(node, onsetTick: 60);

        for (var t = 60; t < 100; t++)
        {
            Assert.That(node.Published[t], Is.EqualTo(lastGoodPublished));
            Assert.That(node.Benchmark![t], Is.EqualTo(lastGoodBenchmark));
        }
    }

    [Test]
    public void Apply_BeforeOnset_SeriesUntouched()
    {
        var before = Node();

        var node = Node();
        QuietMarketInjector.Apply(node, onsetTick: 60);

        Assert.That(node.Published[..60], Is.EqualTo(before.Published[..60]));
        Assert.That(node.Benchmark![..60], Is.EqualTo(before.Benchmark![..60]));
    }

    [Test]
    public void Apply_ReturnsLabelWithNodeAndOnset()
    {
        var label = QuietMarketInjector.Apply(Node(), onsetTick: 60);

        Assert.That(label, Is.EqualTo(new QuietMarketLabel("10Y", 60)));
    }

    [Test]
    public void Apply_NodeWithoutBenchmark_Throws()
    {
        Assert.Throws<ArgumentException>(() => QuietMarketInjector.Apply(Node(withBenchmark: false), onsetTick: 60));
    }

    [Test]
    public void Apply_OnsetBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QuietMarketInjector.Apply(Node(), onsetTick: 0));
    }
}
