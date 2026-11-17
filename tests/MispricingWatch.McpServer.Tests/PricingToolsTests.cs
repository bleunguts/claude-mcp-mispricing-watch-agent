using Microsoft.Extensions.Logging.Abstractions;

namespace MispricingWatch.McpServer.Tests;

public class PricingToolsTests
{
    private static PricingTools Tools() => new(new WorldService(NullLogger<WorldService>.Instance));

    [Test]
    public void Average_TwoWeekWindow_Returns48BucketsOf70Ticks()
    {
        var series = Enumerable.Range(0, 3_360).Select(i => (double)i).ToArray();

        var (start, bucket, values) = Downsampler.Average(series, 3_360);

        Assert.That(values, Has.Length.EqualTo(48));
        Assert.That(bucket, Is.EqualTo(70));
        Assert.That(start, Is.EqualTo(0));
        Assert.That(values[0], Is.EqualTo(34.5));   // mean of ticks 0..69
    }

    [Test]
    public void Average_ShortWindow_UsesTheMostRecentTicks()
    {
        var series = Enumerable.Range(0, 3_360).Select(i => i < 3_120 ? 1.0 : 2.0).ToArray();

        var (start, _, values) = Downsampler.Average(series, 240);

        Assert.That(start, Is.EqualTo(3_120));
        Assert.That(values, Is.All.EqualTo(2.0));
    }

    [Test]
    public void GetHistory_KnownNode_Returns48Values()
    {
        var result = Tools().GetHistory("10Y", "1W");

        Assert.That(result.Available, Is.True);
        Assert.That(result.Values, Has.Length.EqualTo(48));
        Assert.That(result.BucketTicks, Is.EqualTo(25));
    }

    [Test]
    public void GetHistory_UnknownTenor_ReturnsErrorNotException()
    {
        var result = Tools().GetHistory("7Y", "1W");

        Assert.That(result.Error, Does.Contain("unknown tenor"));
        Assert.That(result.Values, Is.Null);
    }

    [Test]
    public void GetHistory_UnknownWindow_ReturnsError()
    {
        Assert.That(Tools().GetHistory("10Y", "3M").Error, Does.Contain("unknown window"));
    }

    [Test]
    public void GetBenchmark_NodeWithNoBenchmark_IsNotAvailableAndNoError()
    {
        var result = Tools().GetBenchmark("3W", "1W");

        Assert.That(result.Available, Is.False);
        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public void GetBenchmark_BenchmarkedNode_LinesUpWithHistoryBuckets()
    {
        var tools = Tools();

        var history = tools.GetHistory("5Y", "2W");
        var benchmark = tools.GetBenchmark("5Y", "2W");

        Assert.That(benchmark.Available, Is.True);
        Assert.That(benchmark.StartTick, Is.EqualTo(history.StartTick));
        Assert.That(benchmark.BucketTicks, Is.EqualTo(history.BucketTicks));
    }
}
