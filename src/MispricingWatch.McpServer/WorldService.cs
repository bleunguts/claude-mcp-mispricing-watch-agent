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
        var quiet = ParseQuiet(Environment.GetEnvironmentVariable("MISPRICING_QUIET"));
        // A quiet world plants no fault: one benchmarked node just stops moving, price and benchmark together.
        var (prices, _) = quiet
            ? WorldBuilder.Build(seed, plantFault: false, quietMarket: true)
            : WorldBuilder.Build(seed, faultType: faultType);
        _nodes = prices.ToDictionary(p => p.Tenor, StringComparer.OrdinalIgnoreCase);
        log.LogInformation("World built with seed {Seed}, {Planted}: {Nodes}", seed,
            quiet ? "quiet market" : $"fault {faultType}", string.Join(", ", _nodes.Keys));
    }

    // MISPRICING_QUIET=true (or 1) serves the quiet-market world instead of a fault. Anything else is off.
    public static bool ParseQuiet(string? value) =>
        value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    // Which fault the world plants comes from server config, like the seed. Anything unset or unknown keeps the default.
    public static FaultType ParseFaultType(string? value) =>
        Enum.TryParse<FaultType>(value, ignoreCase: true, out var f) ? f : FaultType.SettingChange;

    public NodePrices? Find(string tenor) => _nodes.GetValueOrDefault(tenor);
}
