namespace MispricingWatch.CurveLab.Tests;

public static class Utility
{
    public static double[] SpreadBps(NodePrices p) =>
        p.Published.Zip(p.Benchmark!, (pub, bench) => (pub - bench) * 100.0).ToArray();
}
