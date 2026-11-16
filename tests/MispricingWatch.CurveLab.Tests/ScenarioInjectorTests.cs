using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks.Sources;

namespace MispricingWatch.CurveLab.Tests;

public class ScenarioInjectorTests
{
    // Normal market wiggle over these 11 ticks is about 1 bp, so allow 4x that.
    private const int ToleranceBps = 4;
    private const int Seed = 42;
    private const int MonthOfTicks = 12_600; // 21 days x 600 one-minute ticks
    private static double[][] BuildMarketTrueRates(int seed, int ticks)
    {
        return MarketSimulator.GenerateTrueRates(NodeSpec.Mvp, ticks, seed);
    }

    [Test]
    public void ApplyMarketMove_TrueRates_ReturnsLabelWithValidOnsetAndSize()
    {
        // arrange
        var injector = new ScenarioInjector();
        double[][] trueRates = BuildMarketTrueRates(Seed, MonthOfTicks);

        // act 
        var label = injector.ApplyMarketMove(trueRates, Seed);
        Assert.That(label, Is.Not.Null);
        Assert.That(label.OnsetTick, Is.InRange(MonthOfTicks / 10, MonthOfTicks * 9 / 10));
        Assert.That(label.SizeBps, Is.EqualTo(25.0).Or.EqualTo(-25.0));
    }

    [Test]
    public void ApplyMarketMove_TrueRates_MovesAllNodesInLabelDirection()
    {
        // arrange
        var injector = new ScenarioInjector();
        double[][] trueRates = BuildMarketTrueRates(Seed, MonthOfTicks);
        var label = injector.ApplyMarketMove(trueRates, Seed);

        for (int node = 0; node < trueRates.Length; node++)
        {
            // all nodes move the same direction
            int onset = label.OnsetTick;
            var jumpPercent = ElevenTickJumpPercent(trueRates[node], onset);

            // assert
            Assert.That(Math.Sign(jumpPercent), Is.EqualTo(Math.Sign(label.SizeBps)));
        }
    }

    [Test]
    public void ApplyMarketMove_TrueRates_JumpsByLabelSizeWithinTolerance()
    {
        // arrange
        var injector = new ScenarioInjector();
        double[][] trueRates = BuildMarketTrueRates(Seed, MonthOfTicks);               

        // act
        var label = injector.ApplyMarketMove(trueRates, Seed);
        for (int node = 0; node < trueRates.Length; node++)
        {
            // all nodes move within wobble tolerance of the label size
            int onset = label.OnsetTick;
            var jumpPercent = ElevenTickJumpPercent(trueRates[node], onset);

            // assert
            Assert.That(Math.Abs(JumpPercentInBps(jumpPercent)), Is.EqualTo(Math.Abs(label.SizeBps)).Within(ToleranceBps));
        }
    }

    [Test]
    public void ApplyMarketMove_GovernmentNodes_SpreadStaysWithinNormalBandAcrossOnset()
    {
        // arrange
        var injector = new ScenarioInjector();
        double[][] trueRates = BuildMarketTrueRates(Seed, MonthOfTicks);

        // act
        var label = injector.ApplyMarketMove(trueRates, Seed);
        var onset = label.OnsetTick;

        List<NodePrices> nodePrices = PricingSimulator.Generate(NodeSpec.Mvp, trueRates, Seed).ToList();

        foreach (var nodePrice in GovernmentNodePrices(nodePrices))
        {
            var spreadBps = Utility.SpreadBps(nodePrice);
            var window = spreadBps[(onset - 5)..(onset + 6)];

            var bandBps = GetBandBps(); 

            // assert
            Assert.That(window, Is.All.InRange(-bandBps, bandBps), $"Node {nodePrice.Tenor} spread needs to be 4 sigma bps");
        }
    }

    [Test]
    public void ApplyMarketMove_OnlyPublishedPriceShifts_SpreadLeavesBandAtOnset()
    {
        // arrange
        var injector = new ScenarioInjector();
        double[][] trueRates = BuildMarketTrueRates(Seed, MonthOfTicks);

        // act
        var label = injector.ApplyMarketMove(trueRates, Seed);
        var onset = label.OnsetTick;        

        List<NodePrices> nodePrices = BumpPricesForTest(
            PricingSimulator.Generate(NodeSpec.Mvp, trueRates, Seed),
            onset,
            label.SizeBps);

        foreach (var nodePrice in GovernmentNodePrices(nodePrices))
        {
            var spreadBps = Utility.SpreadBps(nodePrice);
            var beforeOnsetWindow = spreadBps[(onset - 5)..onset];
            var afterOnsetWindow = spreadBps[onset..(onset + 6)];
            var bandBps = GetBandBps(); 

            // assert
            Assert.That(beforeOnsetWindow, Is.All.InRange(-bandBps, bandBps), $"Node {nodePrice.Tenor} spread needs to be 4 sigma bps");
            Assert.That(afterOnsetWindow, Is.All.Not.InRange(-bandBps, bandBps), $"Node {nodePrice.Tenor} spread will be out of the 4 sigma bps tolerance");
        }
    }
 
    [Test]
    public void ApplyMarketMove_PublishedPrices_JumpByLabelSizeAcrossOnset()
    {
        // arrange
        var injector = new ScenarioInjector();
        double[][] trueRates = BuildMarketTrueRates(Seed, MonthOfTicks);

        // act
        var label = injector.ApplyMarketMove(trueRates, Seed);
        var onset = label.OnsetTick;        

        List <NodePrices> nodePrices = PricingSimulator.Generate(NodeSpec.Mvp, trueRates, Seed).ToList();

        foreach (var nodePrice in nodePrices)
        {
            // assert            
            var jumpPercent = ElevenTickJumpPercent(nodePrice.Published, onset);
            Assert.That(Math.Sign(jumpPercent), Is.EqualTo(Math.Sign(label.SizeBps)), $"Node {nodePrice.Tenor} published price jump needs to be in the same direction as the label");
            Assert.That(Math.Abs(JumpPercentInBps(jumpPercent)), Is.EqualTo(Math.Abs(label.SizeBps)).Within(ToleranceBps));
        }
    }
    
    [Test]
    public void ApplyMarketMove_DifferentSeeds_ReturnsDifferentLabel()
    {
        // arrange
        var injector = new ScenarioInjector();
        double[][] trueRates1 = BuildMarketTrueRates(Seed, MonthOfTicks);
        double[][] trueRates2 = BuildMarketTrueRates(Seed + 1, MonthOfTicks);
        // act 
        var label1 = injector.ApplyMarketMove(trueRates1, Seed);
        var label2 = injector.ApplyMarketMove(trueRates2, Seed + 1);
        // assert
        Assert.That(label1.OnsetTick, Is.Not.EqualTo(label2.OnsetTick));
        Assert.That(label1.SizeBps, Is.Not.EqualTo(label2.SizeBps));
    }

    [Test]
    public void ApplyMarketMove_SameSeed_ReturnsSameLabel()
    {
        // arrange
        var injector = new ScenarioInjector();
        double[][] trueRates1 = BuildMarketTrueRates(Seed, MonthOfTicks);
        double[][] trueRates2 = BuildMarketTrueRates(Seed, MonthOfTicks);
        // act 
        var label1 = injector.ApplyMarketMove(trueRates1, Seed);
        var label2 = injector.ApplyMarketMove(trueRates2, Seed);
        // assert
        Assert.That(label1.OnsetTick, Is.EqualTo(label2.OnsetTick));
        Assert.That(label1.SizeBps, Is.EqualTo(label2.SizeBps));
    }

    // 100 bps = 1 percent, so 0.25 percent = 25 bps
    private static double JumpPercentInBps(double jumpPercent) 
        => jumpPercent * 100.0;
    private static double ElevenTickJumpPercent(double[] tickRates, int onset) 
        => tickRates[onset + 10] - tickRates[onset - 1];

    // the spread is the difference of two independent wobbles, and independent wobbles combine by squaring, adding,
    // then square-rooting (like the sides of a right-angled triangle): That’s about 0.2236
    private static double SigmaBps() => Math.Sqrt(
                        PricingSimulator.PublishedNoiseBps * PricingSimulator.PublishedNoiseBps +
                        PricingSimulator.GovernmentNoiseBps * PricingSimulator.GovernmentNoiseBps);
    // 4-sigma band, about 0.88 bps
    private static double GetBandBps() => 4 * (double)SigmaBps();

    private static NodePrices[] GovernmentNodePrices(List<NodePrices> nodePrices)
    {
        var governmentTenors = NodeSpec.Mvp.Where(spec => spec.Benchmark == BenchmarkKind.Government).Select(spec => spec.Tenor);
        var governmentNodePrices = nodePrices.Where(np => governmentTenors.Contains(np.Tenor)).ToArray();
        Assert.That(governmentNodePrices.Length, Is.GreaterThan(0));
        return governmentNodePrices;
    }
    private static List<NodePrices> BumpPricesForTest(NodePrices[] nodes, int onset, double shiftInBps) 
        => nodes.Select(nodePrices => nodePrices with
            {
                Published = [.. nodePrices.Published.Select((price, i) => i >= onset ? price + shiftInBps / 100.0 : price)]
            }).ToList();


}
