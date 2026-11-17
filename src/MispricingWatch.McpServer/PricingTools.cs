using System.ComponentModel;
using ModelContextProtocol.Server;

namespace MispricingWatch.McpServer;

/// <summary>
/// What comes back from get_history and get_benchmark. A refusal is a normal result with Error set,
/// never an exception, so a small model can read it and recover.
/// </summary>
public sealed record SeriesResult(
    string? Tenor, string? Window, bool Available, int StartTick, int BucketTicks, double[]? Values, string? Error);

[McpServerToolType]
public class PricingTools(WorldService world)
{
    private const string Tenors = "2Y, 5Y, 10Y, 30Y, 3W";
    private const string Windows = "1D, 1W, 2W";

    [McpServerTool(Name = "ping"), Description("Checks the server is alive. Returns 'pong'.")]
    public static string Ping() => "pong";

    [McpServerTool(Name = "get_history"),
     Description($"The published price of one node, in percent, as 48 averages over the last window. Tenor is one of {Tenors}. Window is one of {Windows}. Each value averages BucketTicks one-minute ticks; StartTick is the tick the first bucket begins at.")]
    public SeriesResult GetHistory(string tenor, string window) => Series(tenor, window, p => p.Published);

    [McpServerTool(Name = "get_benchmark"),
     Description($"The independent benchmark price of one node, in percent, with the same buckets as get_history so the two line up. Tenor is one of {Tenors}. Window is one of {Windows}. Available is false when the node has no benchmark.")]
    public SeriesResult GetBenchmark(string tenor, string window) => Series(tenor, window, p => p.Benchmark);

    private SeriesResult Series(string tenor, string window, Func<CurveLab.NodePrices, double[]?> pick)
    {
        var node = world.Find(tenor);
        if (node is null)
            return Refuse($"unknown tenor '{tenor}'. Use one of: {Tenors}");
        if (!Downsampler.Windows.TryGetValue(window, out var ticks))
            return Refuse($"unknown window '{window}'. Use one of: {Windows}");

        var series = pick(node);
        if (series is null)
            return new SeriesResult(node.Tenor, window, false, 0, 0, null, null);

        var (start, bucket, values) = Downsampler.Average(series, ticks);
        return new SeriesResult(node.Tenor, window, true, start, bucket, values, null);
    }

    private static SeriesResult Refuse(string error) => new(null, null, false, 0, 0, null, error);
}
