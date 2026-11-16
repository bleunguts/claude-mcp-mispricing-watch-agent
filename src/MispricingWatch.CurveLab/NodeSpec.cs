namespace MispricingWatch.CurveLab;

/// <summary>How good the outside opinion of a node's rate is.</summary>
public enum BenchmarkKind
{
    /// <summary>No outside opinion exists for this node (e.g. 3W).</summary>
    None,

    /// <summary>High quality source: tight noise, no delay (e.g. Government of Canada).</summary>
    Government,

    /// <summary>Weaker source: noisier and arrives a few ticks late (e.g. a competitor bank).</summary>
    Competitor,
}

/// <summary>One node on the curve. Rates are in percent, volatility in bps per tick.</summary>
public sealed record NodeSpec(string Tenor, double StartRatePct, double VolBpsPerTick, BenchmarkKind Benchmark)
{
    /// <summary>Hardcoded MVP node set (placeholders, tune later).</summary>
    public static IReadOnlyList<NodeSpec> Mvp { get; } = new[]
    {
        new NodeSpec("3W", 4.00, 0.15, BenchmarkKind.None),
        new NodeSpec("2Y", 3.60, 0.25, BenchmarkKind.Government),
        new NodeSpec("5Y", 3.50, 0.30, BenchmarkKind.Government),
        new NodeSpec("10Y", 3.70, 0.30, BenchmarkKind.Government),
        new NodeSpec("30Y", 3.90, 0.20, BenchmarkKind.Competitor),
    };
}
