using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace MispricingWatch.CurveLab;

public sealed record MarketMoveLabel(int OnsetTick, double SizeBps);
public class ScenarioInjector
{    
    private const double ParallelShiftFixedSize = 25.0;    

    public MarketMoveLabel ApplyMarketMove(double[][] trueRates, int seed)
    {
        // Implementation for applying market move
        var rng = new Random(seed ^ 0xBEEF);
        // precondition: trueRates should always be populated, deliberately not handled
        var ticks = trueRates[0].Length;
        var onset = rng.Next(ticks / 10, ticks * 9 / 10); // keep history before and after
        var sizeBps = rng.Next(2) == 0 ? ParallelShiftFixedSize : -ParallelShiftFixedSize; // up or down, fixed.

        for (var n = 0; n < trueRates.Length; n++)
        {
            for (var t = onset; t < ticks; t++)
            {
                // mutate the true rates in place, to simulate a market move.
                // the published and benchmark will be generated from this.
                trueRates[n][t] += sizeBps / 100.0; // bps to percent
            }
        }

        return new MarketMoveLabel(onset, sizeBps);
    }
}
