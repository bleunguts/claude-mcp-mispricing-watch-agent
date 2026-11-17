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
    public void ApplySettingChange_DurationPastEnd_LabelClampedToEndOfSeries()
    {
        var node = BuildNode();

        var label = FaultInjector.ApplySettingChange(node, Onset, int.MaxValue, 5.0);

        Assert.That(label, Is.EqualTo(new FaultLabel("2Y", Onset, Ticks - Onset, 5.0)));
    }
}
