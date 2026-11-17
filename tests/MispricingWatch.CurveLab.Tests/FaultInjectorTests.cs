namespace MispricingWatch.CurveLab.Tests;

public class FaultInjectorTests
{
    private const int Seed = 42;
    private const int Ticks = 2_000;
    private const int Onset = 500;
    private const int Duration = 100;

    private static NodePrices BuildNode()   // 2Y, has a government benchmark
    {
        var trueRates = MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, Ticks, Seed);
        return PricingSimulator.Generate(NodeSpec.Mvp, trueRates, Seed)[1];
    }

    [Test]
    public void ApplySettingChange_Node_OffsetsPublishedInsideIntervalOnly()
    {
        var node = BuildNode();
        var healthy = (double[])node.Published.Clone();

        FaultInjector.ApplySettingChange(node, Onset, Duration, 5.0);

        for (var t = 0; t < Ticks; t++)
        {
            var expected = t >= Onset && t < Onset + Duration ? healthy[t] + 0.05 : healthy[t]; // 5 bps = 0.05 percent
            Assert.That(node.Published[t], Is.EqualTo(expected).Within(1e-9));
        }
    }

    [Test]
    public void ApplySettingChange_Node_LeavesBenchmarkUntouched()
    {
        var node = BuildNode();
        var benchmark = (double[])node.Benchmark!.Clone();

        FaultInjector.ApplySettingChange(node, Onset, Duration, 5.0);

        Assert.That(node.Benchmark, Is.EqualTo(benchmark));
    }

    [Test]
    public void ApplyStalePrice_Node_HoldsLastGoodValueInsideIntervalOnly()
    {
        var node = BuildNode();
        var healthy = (double[])node.Published.Clone();

        FaultInjector.ApplyStalePrice(node, Onset, Duration);

        for (var t = 0; t < Ticks; t++)
        {
            var expected = t >= Onset && t < Onset + Duration ? healthy[Onset - 1] : healthy[t];
            Assert.That(node.Published[t], Is.EqualTo(expected).Within(1e-9));
        }
    }

    [Test]
    public void ApplyStalePrice_Node_LeavesBenchmarkUntouched()
    {
        var node = BuildNode();
        var benchmark = (double[])node.Benchmark!.Clone();

        FaultInjector.ApplyStalePrice(node, Onset, Duration);

        Assert.That(node.Benchmark, Is.EqualTo(benchmark));
    }

    [Test]
    public void ApplyStalePrice_NoDuration_LabelRunsToEndOfSeries()
    {
        var node = BuildNode();

        var label = FaultInjector.ApplyStalePrice(node, Onset, durationTicks: null);

        Assert.That(label, Is.EqualTo(new FaultLabel("2Y", Onset, Ticks - Onset, 0, FaultType.StalePrice)));
    }

    [Test]
    public void ApplySettingChange_NoDuration_LabelRunsToEndOfSeries()
    {
        var node = BuildNode();

        var label = FaultInjector.ApplySettingChange(node, Onset, durationTicks: null, 5.0);

        Assert.That(label, Is.EqualTo(new FaultLabel("2Y", Onset, Ticks - Onset, 5.0)));
    }

    [Test]
    public void ApplySettingChange_DurationPastEnd_LabelClampedToEndOfSeries()
    {
        var node = BuildNode();

        var label = FaultInjector.ApplySettingChange(node, Onset, Ticks, 5.0); // Onset + Ticks is beyond the end

        Assert.That(label, Is.EqualTo(new FaultLabel("2Y", Onset, Ticks - Onset, 5.0)));
    }
}
