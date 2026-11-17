namespace MispricingWatch.CurveLab;

/// <summary>The hidden answer for one planted fault. Never reaches the agent.</summary>
public sealed record FaultLabel(string Tenor, int OnsetTick, int DurationTicks, double OffsetBps);

public static class FaultInjector
{
    /// <summary>
    /// Setting change: from onsetTick the published price sits offsetBps away from where it
    /// should be, for durationTicks (clamped to the end). Edits the published series in place;
    /// the benchmark is never touched.
    /// </summary>
    public static FaultLabel ApplySettingChange(NodePrices node, int onsetTick, int durationTicks, double offsetBps)
    {
        var end = (int)Math.Min((long)onsetTick + durationTicks, node.Published.Length);
        for (var t = onsetTick; t < end; t++)
            node.Published[t] += offsetBps / 100.0; // bps to percent

        return new FaultLabel(node.Tenor, onsetTick, end - onsetTick, offsetBps);
    }
}
