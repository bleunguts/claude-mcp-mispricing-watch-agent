using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MispricingWatch.Agent;

// Sends alarms out of the process as OpenTelemetry log records.The exporters are passed in,
// so the program picks console or OTLP and a test picks an in-memory one.
public class AlarmTelemetry : IDisposable
{
    private readonly ILogger<AlarmTelemetry> _logger;
    private ILoggerFactory _factory;

    public AlarmTelemetry(Action<OpenTelemetryLoggerOptions> addExporters)
    {
        _factory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MispricingWatch.Agent"));
            options.IncludeFormattedMessage = true;
            addExporters(options);
        }));
        _logger = _factory.CreateLogger<AlarmTelemetry>();
    }

    public void Emit(AlarmRecord record)
    {
        if (!record.Alarm) return;   // only alarms leave the process, not OK verdicts

        var attributes = new Dictionary<string, object>
        {
            { "mispricing.node", record.Node },
            { "mispricing.detector", record.Detector },
            { "mispricing.rationale", record.Rationale },
            { "mispricing.prompt_version", record.PromptVersion }
        };  
        _logger.Log(LogLevel.Warning, default, attributes, null, (_, _) => $"Mispricing Alarm on {record.Node}");
    }

    public void Dispose()
    {
        _factory.Dispose();      
    }
}
