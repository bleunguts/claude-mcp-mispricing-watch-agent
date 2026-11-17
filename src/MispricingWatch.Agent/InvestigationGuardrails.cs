using System.Text.Json;

namespace MispricingWatch.Agent;

// The rules an investigation answer must satisfy. They run in code because a small model ignores the prompt:
// Qwen 3b answered ALARM on a score of -0.768 with 0 ticks outside, after skipping get_benchmark.
public static class InvestigationGuardrails
{
    private static readonly string[] RequiredSteps = ["get_history", "get_benchmark", "compute_spread_stats"];

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

        // 3. Agreement with the measurement: outside tolerance means alarm, inside means OK.
        var stats = executions.LastOrDefault(e => e.Name == "compute_spread_stats" && !e.IsError);
        if (stats != null && ExpectedAlarm(stats.Result) is (bool expected, double gap, double tolerance) && expected != record.Alarm)
        {
            return $"compute_spread_stats shows currentSpreadBps {gap} against toleranceBps {tolerance}, " +
                   $"so the verdict must be {(expected ? "ALARM" : "OK")}. Check your verdict against these figures.";
        }
        return null;
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
