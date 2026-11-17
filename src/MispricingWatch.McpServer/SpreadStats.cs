using MispricingWatch.CurveLab;

namespace MispricingWatch.McpServer;

/// <summary>Detector settings. They come from server config, never from the caller.</summary>
public sealed record SpreadParams(double K = 4.0, double FloorBps = 1.0, int CalibrationTicks = 1_200)
{
    public static SpreadParams FromEnvironment() => new(
        K: double.TryParse(Environment.GetEnvironmentVariable("SPREAD_K"), out var k) ? k : 4.0,
        FloorBps: double.TryParse(Environment.GetEnvironmentVariable("SPREAD_FLOOR_BPS"), out var f) ? f : 1.0);
}

/// <summary>Evidence about one node's gap to its benchmark. Not a verdict.</summary>
public sealed record SpreadStatsResult(
    string? Tenor, bool Available, int CalibrationTicks, double CenterBps, double SigmaBps, double ToleranceBps,
    double CurrentSpreadBps, double Score, int TicksOutside, bool OutsideTolerance, string? Error);

public static class SpreadStats
{
    private const double MadToSigma = 1.4826;   // makes MAD comparable to a standard deviation for normal noise

    public static SpreadStatsResult Compute(NodePrices node, SpreadParams p)
    {
        if (node.Benchmark is null)
            return new SpreadStatsResult(node.Tenor, false, 0, 0, 0, 0, 0, 0, 0, false, null);

        // spread in bps, per tick
        var spread = node.Published.Zip(node.Benchmark, (pub, bench) => (pub - bench) * 100.0).ToArray();

        // Normal for this node: the first CalibrationTicks, which stop well before any planted fault.
        // Median and MAD, so a few odd ticks cannot move them the way they move a mean and standard deviation.
        var calibration = spread.Take(p.CalibrationTicks).ToArray();
        var center = Median(calibration);
        var sigma = MadToSigma * Median(calibration.Select(x => Math.Abs(x - center)).ToArray());
        var tolerance = Math.Max(p.K * sigma, p.FloorBps);

        var latest = spread[^1];
        var score = (latest - center) / Math.Max(sigma, 1e-6);

        // Persistence: trailing ticks in a row beyond tolerance, never reaching back into the calibration window.
        var ticksOutside = 0;
        for (var t = spread.Length - 1; t >= p.CalibrationTicks && Math.Abs(spread[t] - center) > tolerance; t--)
            ticksOutside++;

        return new SpreadStatsResult(
            node.Tenor, true, p.CalibrationTicks, Round(center), Round(sigma), Round(tolerance),
            Round(latest), Round(score), ticksOutside, Math.Abs(latest - center) > tolerance, null);
    }

    private static double Round(double x) => Math.Round(x, 3);

    private static double Median(double[] values)
    {
        var sorted = values.Order().ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
