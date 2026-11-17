namespace MispricingWatch.McpServer;

/// <summary>
/// Turns the last N ticks of a series into a fixed number of bucket averages, so a tool result is the
/// same small size whatever the window (a small local model has a context of only about 8k tokens).
/// </summary>
public static class Downsampler
{
    public const int Points = 48;

    // Named windows, in ticks (240 ticks a day, 5 trading days a week).
    public static readonly IReadOnlyDictionary<string, int> Windows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["1D"] = 240,
        ["1W"] = 1_200,
        ["2W"] = 3_360,
    };

    /// <summary>Averages the last <paramref name="windowTicks"/> ticks into <see cref="Points"/> buckets.</summary>
    public static (int StartTick, int BucketTicks, double[] Values) Average(double[] series, int windowTicks)
    {
        var bucket = windowTicks / Points;
        var start = series.Length - windowTicks;
        var values = new double[Points];
        for (var i = 0; i < Points; i++)
        {
            values[i] = Math.Round(series.Skip(start + i * bucket).Take(bucket).Average(), 4);
        }
        return (start, bucket, values);
    }
}
