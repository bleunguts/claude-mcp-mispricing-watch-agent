namespace MispricingWatch.CurveLab;

/// <summary>One node on the curve. Rates are in percent, volatility in bps per tick.</summary>
public sealed record NodeSpec(string Tenor, double StartRatePct, double VolBpsPerTick)
{
    /// <summary>Hardcoded MVP node set (placeholders, tune later).</summary>
    public static IReadOnlyList<NodeSpec> Mvp { get; } = new[]
    {
        new NodeSpec("3W", 4.00, 0.15),
        new NodeSpec("2Y", 3.60, 0.25),
        new NodeSpec("5Y", 3.50, 0.30),
        new NodeSpec("10Y", 3.70, 0.30),
        new NodeSpec("30Y", 3.90, 0.20),
    };
}
