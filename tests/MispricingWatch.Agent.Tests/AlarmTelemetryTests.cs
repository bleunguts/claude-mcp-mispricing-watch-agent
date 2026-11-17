using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using System;
using System.Collections.Generic;
using System.Text;

namespace MispricingWatch.Agent.Tests;
public class AlarmTelemetryTests
{
    [Test]
    public void Emit_AlarmRecord_ExportsWarningWithAgreedAttributes()
    {
        var exported = new List<LogRecord>();
        using (var telemetry = new AlarmTelemetry(o => o.AddInMemoryExporter(exported)))
        {
            telemetry.Emit(new AlarmRecord("30Y", true, "spread-to-benchmark", "score 5.955", "v1"));
        }

        Assert.That(exported, Has.Count.EqualTo(1));
        Assert.That(exported[0].LogLevel, Is.EqualTo(LogLevel.Warning));
        var attrs = exported[0].Attributes!.ToDictionary(a => a.Key, a => a.Value);
        Assert.That(attrs["mispricing.node"], Is.EqualTo("30Y"));
        Assert.That(attrs["mispricing.detector"], Is.EqualTo("spread-to-benchmark"));
        Assert.That(attrs["mispricing.rationale"], Is.EqualTo("score 5.955"));
        Assert.That(attrs["mispricing.prompt_version"], Is.EqualTo("v1"));
    }

    [Test]
    public void Emit_OkRecord_ExportsNothing()
    {
        var exported = new List<LogRecord>();
        using (var telemetry = new AlarmTelemetry(o => o.AddInMemoryExporter(exported)))
        {
            telemetry.Emit(new AlarmRecord("10Y", false, "none", "inside tolerance", "v1"));
        }

        Assert.That(exported, Is.Empty);
    }

    [Test]
    public void Emit_AlarmRecord_MessageNamesTheNode()
    {
        var exported = new List<LogRecord>();
        using (var telemetry = new AlarmTelemetry(o => o.AddInMemoryExporter(exported)))
        {
            telemetry.Emit(new AlarmRecord("30Y", true, "spread-to-benchmark", "score 5.955", "v1"));
        }

        Assert.That(exported[0].FormattedMessage, Does.Contain("30Y"));
    }
}
