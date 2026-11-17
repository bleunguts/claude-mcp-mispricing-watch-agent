using System.Text.Json;
using System.Text.RegularExpressions;

namespace MispricingWatch.Agent;

// What a run produces: a decision somebody can act on or log. Printed as JSON for now (OpenTelemetry is #34).
public record AlarmRecord(string Node, bool Alarm, string Detector, string Rationale, string PromptVersion)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    // Reads the model's "Verdict:" and "Rationale:" lines. Null when there is no usable verdict line, which
    // the caller reports as INCOMPLETE rather than guessing.
    public static AlarmRecord? Parse(string answer, string node)
    {
        var verdict = Regex.Match(answer, @"^\s*Verdict:\s*(ALARM|OK)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (!verdict.Success)
        {
            return null;
        }
        var alarm = verdict.Groups[1].Value.Equals("ALARM", StringComparison.OrdinalIgnoreCase);
        var rationale = Regex.Match(answer, @"^\s*Rationale:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return new AlarmRecord(
            node, alarm, alarm ? "spread-to-benchmark" : "none",
            rationale.Success ? rationale.Groups[1].Value.Trim() : "", InvestigationPrompt.Version);
    }
}
