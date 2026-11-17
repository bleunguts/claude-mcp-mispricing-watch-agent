namespace MispricingWatch.CurveLab;

/// <summary>The hidden answer for a quiet spell: no fault, the node just stopped moving. Never reaches the agent.</summary>
public sealed record QuietMarketLabel(string Tenor, int OnsetTick);

public static class QuietMarketInjector
{
    /// <summary>
    /// Quiet market: from onsetTick both the published price and its benchmark hold their last good value, to the end
    /// of the series. A flat price next to a flat benchmark is a market that stopped moving, not a defect, so no
    /// alarm is expected. Edits both series in place. Needs a benchmark, since the point is that it is flat too.
    /// </summary>
    public static QuietMarketLabel Apply(NodePrices node, int onsetTick)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(onsetTick, 1); // needs a last good value before onset
        if (node.Benchmark is null)
        {
            throw new ArgumentException("A quiet market needs a benchmark that also goes flat.", nameof(node));
        }

        var frozenPublished = node.Published[onsetTick - 1];
        var frozenBenchmark = node.Benchmark[onsetTick - 1];
        for (var t = onsetTick; t < node.Published.Length; t++)
        {
            node.Published[t] = frozenPublished;
            node.Benchmark[t] = frozenBenchmark;
        }
        return new QuietMarketLabel(node.Tenor, onsetTick);
    }
}
