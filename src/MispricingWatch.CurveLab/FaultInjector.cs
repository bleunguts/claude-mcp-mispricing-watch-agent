namespace MispricingWatch.CurveLab;

/// <summary>The hidden answer for one planted fault. Never reaches the agent.</summary>
public sealed record FaultLabel(
    string Tenor, int OnsetTick, int DurationTicks, double OffsetBps, FaultType Type = FaultType.SettingChange);

public enum FaultType { SettingChange, StalePrice }

public static class FaultInjector
{
    /// <summary>
    /// Setting change: from onsetTick the published price sits offsetBps away from where it
    /// should be. It lasts durationTicks, or until the end of the series when durationTicks is
    /// null (a fault that never recovers). Edits the published series in place; the benchmark is
    /// never touched.
    /// </summary>
    public static FaultLabel ApplySettingChange(NodePrices node, int onsetTick, int? durationTicks, double offsetBps)
    {
        var end = EndTick(node, onsetTick, durationTicks);
        for (var t = onsetTick; t < end; t++)
            node.Published[t] += offsetBps / 100.0; // bps to percent

        return new FaultLabel(node.Tenor, onsetTick, end - onsetTick, offsetBps);
    }

    /// <summary>
    /// Stale price: from onsetTick the published value holds its last good value while the
    /// benchmark keeps moving. It lasts durationTicks, or until the end of the series when
    /// durationTicks is null. There is no gap at first; it grows as the market drifts away from
    /// the frozen value. Edits the published series in place.
    /// </summary>
    public static FaultLabel ApplyStalePrice(NodePrices node, int onsetTick, int? durationTicks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(onsetTick, 1); // needs a last good value before onset
        var end = EndTick(node, onsetTick, durationTicks);
        var frozen = node.Published[onsetTick - 1];
        for (var t = onsetTick; t < end; t++)
            node.Published[t] = frozen;

        return new FaultLabel(node.Tenor, onsetTick, end - onsetTick, 0, FaultType.StalePrice);
    }

    // Works out where the fault stops: the tick just after the last faulty one.
    private static int EndTick(NodePrices node, int onsetTick, int? durationTicks)
    {
        int lastTickOfSeries = node.Published.Length;

        // No duration given: the fault never recovers, so it runs to the end of the series.
        if (durationTicks == null)
        {
            return lastTickOfSeries;
        }

        // A duration was given: the fault stops that many ticks after it started...
        int stopTick = onsetTick + durationTicks.Value;

        // ...unless that would be past the end of the series, in which case it stops at the end.
        if (stopTick > lastTickOfSeries)
        {
            return lastTickOfSeries;
        }
        return stopTick;
    }
}
