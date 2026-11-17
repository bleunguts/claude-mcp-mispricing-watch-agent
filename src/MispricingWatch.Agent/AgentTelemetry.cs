using System.Diagnostics;

namespace MispricingWatch.Agent;

// One shared source for every span the agent creates. The tracer provider listens to it by name.
public static class AgentTelemetry
{
    public const string SourceName = "MispricingWatch.Agent";
    public static readonly ActivitySource Source = new(SourceName);
}
