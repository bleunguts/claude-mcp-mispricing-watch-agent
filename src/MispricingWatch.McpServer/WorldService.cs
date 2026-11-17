using Microsoft.Extensions.Logging;
using MispricingWatch.CurveLab;

namespace MispricingWatch.McpServer;

/// <summary>
/// Holds the simulated prices the tools serve. The seed comes from server config, never from the caller.
/// The ground truth that WorldBuilder returns is dropped here on purpose: the tools must not be able to leak it.
/// </summary>
public sealed class WorldService
{
    public const int DefaultSeed = 42;

    private readonly Dictionary<string, NodePrices> _nodes;

    public WorldService(ILogger<WorldService> log)
    {
        var seed = int.TryParse(Environment.GetEnvironmentVariable("MISPRICING_SEED"), out var s) ? s : DefaultSeed;
        var faultType = ParseFaultType(Environment.GetEnvironmentVariable("MISPRICING_FAULT"));
        var (prices, _) = WorldBuilder.Build(seed, faultType: faultType);
        _nodes = prices.ToDictionary(p => p.Tenor, StringComparer.OrdinalIgnoreCase);
        log.LogInformation("World built with seed {Seed}, fault {Fault}: {Nodes}", seed, faultType, string.Join(", ", _nodes.Keys));
    }

    // Which fault the world plants comes from server config, like the seed. Anything unset or unknown keeps the default.
    public static FaultType ParseFaultType(string? value) =>
        Enum.TryParse<FaultType>(value, ignoreCase: true, out var f) ? f : FaultType.SettingChange;

    public NodePrices? Find(string tenor) => _nodes.GetValueOrDefault(tenor);
}
