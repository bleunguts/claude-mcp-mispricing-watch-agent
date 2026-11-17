using System.Reflection;

namespace MispricingWatch.CurveLab.Tests;

public class WorldBuilderTests
{
    private const int Seed = 42;

    [Test]
    public void Build_SameSeed_IdenticalWorld()
    {
        var (pricesA, truthA) = WorldBuilder.Build(Seed);
        var (pricesB, truthB) = WorldBuilder.Build(Seed);

        Assert.That(truthA, Is.EqualTo(truthB));
        for (var n = 0; n < pricesA.Length; n++)
            Assert.That(pricesA[n].Published, Is.EqualTo(pricesB[n].Published));
    }

    [Test]
    public void Build_NoFault_TruthHasNoFault()
    {
        var (_, truth) = WorldBuilder.Build(Seed, plantFault: false);

        Assert.That(truth.Fault, Is.Null);
    }

    [Test]
    public void Build_PlantFault_NeverOnNodeWithoutBenchmark()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var (_, truth) = WorldBuilder.Build(seed);

            Assert.That(truth.Fault!.Tenor, Is.Not.EqualTo("3W"));
        }
    }

    [Test]
    public void Build_PlantFault_GapJumpsByLabelOffsetAtLabelOnset()
    {
        var (prices, truth) = WorldBuilder.Build(Seed);
        var fault = truth.Fault!;
        var node = prices.Single(p => p.Tenor == fault.Tenor);

        double MeanGapBps(int from, int to) =>
            Enumerable.Range(from, to - from).Average(t => (node.Published[t] - node.Benchmark![t]) * 100.0);

        var jump = MeanGapBps(fault.OnsetTick, node.Published.Length) - MeanGapBps(0, fault.OnsetTick);

        Assert.That(jump, Is.EqualTo(WorldBuilder.FaultOffsetBps).Within(0.5));
    }

    [Test]
    public void Build_StalePriceType_PlantsFrozenPublishedPriceWithMatchingLabel()
    {
        var (prices, truth) = WorldBuilder.Build(Seed, faultType: FaultType.StalePrice);
        var fault = truth.Fault!;
        var node = prices.Single(p => p.Tenor == fault.Tenor);

        Assert.That(fault.Type, Is.EqualTo(FaultType.StalePrice));
        for (var t = fault.OnsetTick; t < node.Published.Length; t++)
            Assert.That(node.Published[t], Is.EqualTo(node.Published[fault.OnsetTick - 1]));
    }

    [Test]
    public void Build_DefaultType_IsSettingChange()
    {
        var (_, truth) = WorldBuilder.Build(Seed);

        Assert.That(truth.Fault!.Type, Is.EqualTo(FaultType.SettingChange));
    }

    [Test]
    public void NodePrices_PublicShape_ExposesNoLabelTypes()
    {
        var labelTypes = new[] { typeof(FaultLabel), typeof(MarketMoveLabel), typeof(GroundTruth) };
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var memberTypes = typeof(NodePrices).GetProperties(flags).Select(p => p.PropertyType)
            .Concat(typeof(NodePrices).GetFields(flags).Select(f => f.FieldType));

        Assert.That(memberTypes.Intersect(labelTypes), Is.Empty);
    }
}
