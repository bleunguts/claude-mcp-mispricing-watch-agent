using MispricingWatch.CurveLab;

namespace MispricingWatch.McpServer.Tests;

// =====================================================================================================
//   !!  SECRET GROUND TRUTH  !!   !!  SECRET GROUND TRUTH  !!   !!  SECRET GROUND TRUTH  !!
//
//   This is the ONLY place that peeks at the answer key (GroundTruth: which node was really faulted).
//   Tools, the server and the agent must never see it. We peek here, on purpose, to mark the detectors' homework.
//   If you find yourself reading GroundTruth anywhere else, that is a leak: stop.
// =====================================================================================================
//
// Scorecard (#36). For each seed it builds one world WITH a planted fault and one WITHOUT, asks each detector
// "alarm or not?" for every benchmarked node at the end of the window, and checks the answers against the
// SECRET truth. The fault never ends, so it is still active at the end: an alarm on the faulted node is a hit,
// an alarm on any other node (or in a clean world) is a false alarm, and silence on the faulted node is a miss.
// To see the table: dotnet test --filter SecretGroundTruth --logger "console;verbosity=detailed"
[Category("SecretGroundTruth")]
public class SecretGroundTruthScoreTests
{
    private const int Seeds = 100;
    private const double FixedThresholdBps = 2.0;   // the naive baseline: one gap for every node, no calibration
    private const double TargetMinPrecision = 0.80; // the pass marks, from the requirements (not ground truth)
    private const double TargetMaxMissRate = 0.05;

    private sealed class Counts
    {
        public int Hits, FalseAlarms, Misses;
        public double Precision => Hits + FalseAlarms == 0 ? 1 : (double)Hits / (Hits + FalseAlarms);
        public double MissRate => Hits + Misses == 0 ? 0 : (double)Misses / (Hits + Misses);
    }

    private static Counts ScoreAgainstSecretGroundTruth(Func<NodePrices, bool> alarms, FaultType faultType)
    {
        var counts = new Counts();
        for (var seed = 1; seed <= Seeds; seed++)
        {
            foreach (var plantFault in new[] { true, false })
            {
                var (prices, secretGroundTruth) = WorldBuilder.Build(seed, plantFault, faultType);   // <-- the answer key comes out here
                foreach (var node in prices.Where(p => p.Benchmark is not null))
                {
                    var isReallyFaulty_PeekedFromGroundTruth = secretGroundTruth.Fault?.Tenor == node.Tenor;   // <-- peeking
                    var alarm = alarms(node);
                    if (alarm && isReallyFaulty_PeekedFromGroundTruth) counts.Hits++;
                    else if (alarm) counts.FalseAlarms++;
                    else if (isReallyFaulty_PeekedFromGroundTruth) counts.Misses++;
                }
            }
        }
        return counts;
    }

    private static string Row(string name, Counts c) =>
        $"{name,-30} {c.Hits,5} {c.FalseAlarms,6} {c.Misses,7} {c.Precision,10:P1} {c.MissRate,10:P1}";

    private static readonly SpreadParams Spread = new();

    private static bool SpreadAlarms(NodePrices node) => SpreadStats.Compute(node, Spread).OutsideTolerance;

    private static bool FixedThresholdAlarms(NodePrices node) =>
        Math.Abs((node.Published[^1] - node.Benchmark![^1]) * 100.0) > FixedThresholdBps;

    private static readonly StaleParams Stale = new();

    private static bool StaleAlarms(NodePrices node) => StaleCheck.Compute(node, Stale).Stale;

    // Two detectors together: alarm if either one alarms.
    private static bool SpreadOrStaleAlarms(NodePrices node) => SpreadAlarms(node) || StaleAlarms(node);

    private static Counts PrintTable(FaultType faultType)
    {
        var spread = ScoreAgainstSecretGroundTruth(SpreadAlarms, faultType);
        var fixedThreshold = ScoreAgainstSecretGroundTruth(FixedThresholdAlarms, faultType);
        var stale = ScoreAgainstSecretGroundTruth(StaleAlarms, faultType);
        var spreadOrStale = ScoreAgainstSecretGroundTruth(SpreadOrStaleAlarms, faultType);

        Console.WriteLine($"*** SECRET GROUND TRUTH SCORECARD, fault type {faultType} (answer key peeked at on purpose) ***");
        Console.WriteLine($"{Seeds} seeds, each as a faulted and a clean world; 4 benchmarked nodes checked per world.");
        Console.WriteLine($"{"detector",-30} {"hits",5} {"false",6} {"missed",7} {"precision",10} {"miss rate",10}");
        Console.WriteLine(Row("spread (per-node tolerance)", spread));
        Console.WriteLine(Row($"fixed threshold ({FixedThresholdBps} bps)", fixedThreshold));
        Console.WriteLine(Row("stale check (frozen price)", stale));
        Console.WriteLine(Row("spread or stale", spreadOrStale));
        Console.WriteLine($"Targets: precision at least {TargetMinPrecision:P0}, miss rate at most {TargetMaxMissRate:P0}.");
        return spreadOrStale;
    }

    // The detector we ship is "spread or stale". It is held to the targets on both fault types;
    // the other rows are there to compare against.
    [Test]
    public void SecretGroundTruthScore_SettingChange_SpreadOrStaleMeetsTargets()
    {
        var spreadOrStale = PrintTable(FaultType.SettingChange);

        Assert.That(spreadOrStale.Precision, Is.GreaterThanOrEqualTo(TargetMinPrecision));
        Assert.That(spreadOrStale.MissRate, Is.LessThanOrEqualTo(TargetMaxMissRate));
    }

    [Test]
    public void SecretGroundTruthScore_StalePrice_SpreadOrStaleMeetsTargets()
    {
        var spreadOrStale = PrintTable(FaultType.StalePrice);

        Assert.That(spreadOrStale.Precision, Is.GreaterThanOrEqualTo(TargetMinPrecision));
        Assert.That(spreadOrStale.MissRate, Is.LessThanOrEqualTo(TargetMaxMissRate));
    }
}
