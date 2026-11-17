using System.Text.Json;

namespace MispricingWatch.Agent;

// The rules an investigation answer must satisfy. They run in code because a small model ignores the prompt:
// Qwen 3b answered ALARM on a score of -0.768 with 0 ticks outside, after skipping get_benchmark.
public static class InvestigationGuardrails
{
    private static readonly string[] RequiredSteps = ["get_history", "get_benchmark", "compute_spread_stats", "check_stale"];

    // Returns what is wrong with the answer, worded as an instruction to the model, or null when it is fine.
    public static string? Problem(IReadOnlyList<ToolExecution> executions, string answer)
    {
        // 1. Required steps: every tool must have really succeeded.
        foreach (var step in RequiredSteps)
        {
            if (!executions.Any(e => e.Name == step && !e.IsError))
            {
                return $"Call {step} before you answer.";
            }
        }

        // 2. Answer format: a Verdict line the alarm record can read.
        var record = AlarmRecord.Parse(answer, "");
        if (record == null)
        {
            return "End your answer with the line 'Verdict: ALARM' or 'Verdict: OK', then a line starting 'Rationale:'.";
        }

        // 3. Agreement with the measurements: alarm when the spread is outside tolerance OR the price is frozen.
        var stats = executions.LastOrDefault(e => e.Name == "compute_spread_stats" && !e.IsError);
        var staleCheck = executions.LastOrDefault(e => e.Name == "check_stale" && !e.IsError);
        var spread = stats == null ? null : ExpectedAlarm(stats.Result);
        bool isStale = staleCheck != null && IsStale(staleCheck.Result);

        if (spread == null && !isStale)
        {
            return null; // nothing measured to compare the verdict against
        }

        bool expectedAlarm = spread?.Alarm == true || isStale;
        if (expectedAlarm == record.Alarm)
        {
            return null;
        }
        if (isStale)
        {
            return "check_stale reports the price is stale (frozen while the benchmark moved), " +
                   "so the verdict must be ALARM. Check your verdict against this result.";
        }
        return $"compute_spread_stats shows currentSpreadBps {spread!.Value.GapBps} against toleranceBps {spread.Value.ToleranceBps}, " +
               $"so the verdict must be {(spread.Value.Alarm ? "ALARM" : "OK")}. Check your verdict against these figures.";
    }

    // Whether check_stale reported a frozen price. A missing or non-boolean field counts as not stale.
    public static bool IsStale(string staleJson)
    {
        using var doc = JsonDocument.Parse(staleJson);
        return doc.RootElement.TryGetProperty("stale", out var stale) && stale.ValueKind == JsonValueKind.True;
    }

    // The detector's own answer, from the fields compute_spread_stats returned. Null when there is no benchmark.
    public static (bool Alarm, double GapBps, double ToleranceBps)? ExpectedAlarm(string statsJson)
    {
        using var doc = JsonDocument.Parse(statsJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("available", out var available) || !available.GetBoolean())
        {
            return null;
        }
        var gap = root.GetProperty("currentSpreadBps").GetDouble();
        var tolerance = root.GetProperty("toleranceBps").GetDouble();
        return (Math.Abs(gap) > tolerance, gap, tolerance);
    }
}
