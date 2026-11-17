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
        var (prices, _) = WorldBuilder.Build(seed);
        _nodes = prices.ToDictionary(p => p.Tenor, StringComparer.OrdinalIgnoreCase);
        log.LogInformation("World built with seed {Seed}: {Nodes}", seed, string.Join(", ", _nodes.Keys));
    }

    public NodePrices? Find(string tenor) => _nodes.GetValueOrDefault(tenor);
}
