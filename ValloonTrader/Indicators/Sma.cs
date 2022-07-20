using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valloon.Indicators
{
    [Serializable]
    public class SmaResult
    {
        public DateTime Timestamp { get; set; }
        public float? Sma { get; set; }
    }

    /*
     * https://github.com/DaveSkender/Stock.Indicators/blob/main/src/m-r/MaEnvelopes/MaEnvelopes.cs
     */
    public class Sma
    {
        // SIMPLE MOVING AVERAGE
        public static IEnumerable<SmaResult> GetSma(
            IEnumerable<CandleQuote> quotes,
            int lookbackPeriods)
        {
            // calculate
            return CalcSma(quotes.ToList(), lookbackPeriods);
        }

        // internals
        private static List<SmaResult> CalcSma(List<CandleQuote> quotesList, int lookbackPeriods)
        {
            // check parameter arguments
            ValidateSma(lookbackPeriods);

            var results = new List<SmaResult>(quotesList.Count);

            for (int i = 0; i < quotesList.Count; i++)
            {
                var q = quotesList[i];
                SmaResult result = new SmaResult
                {
                    Timestamp = q.Timestamp
                };
                if (i >= lookbackPeriods - 1)
                {
                    float[] closeArray = new float[lookbackPeriods];
                    for (int j = 0; j < lookbackPeriods; j++)
                        closeArray[j] = quotesList[i - lookbackPeriods + 1 + j].Close;
                    result.Sma = closeArray.Average();
                }
                results.Add(result);
            }

            //float? prevValue = null;

            //// roll through quotes
            //for (int i = 0; i < quotesList.Count; i++)
            //{
            //    var q = quotesList[i];
            //    SmaResult result = new SmaResult
            //    {
            //        Timestamp = q.Timestamp
            //    };

            //    // calculate SMMA
            //    if (i + 1 > lookbackPeriods)
            //    {
            //        result.Sma = ((prevValue * (lookbackPeriods - 1)) + q.Close)
            //                    / lookbackPeriods;
            //    }

            //    // first SMMA calculated as simple SMA
            //    else if (i + 1 == lookbackPeriods)
            //    {
            //        float? sumClose = 0;
            //        for (int p = i + 1 - lookbackPeriods; p <= i; p++)
            //        {
            //            var d = quotesList[p];
            //            sumClose += d.Close;
            //        }

            //        result.Sma = sumClose / lookbackPeriods;
            //    }

            //    prevValue = result.Sma;
            //    results.Add(result);
            //}

            return results;
        }

        // parameter validation
        private static void ValidateSma(
        int lookbackPeriods)
        {
            // check parameter arguments
            if (lookbackPeriods <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lookbackPeriods), lookbackPeriods,
                    "Lookback periods must be greater than 0 for SMA.");
            }
        }
    }
}
