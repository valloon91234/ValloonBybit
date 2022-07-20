using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valloon.Indicators
{
    [Serializable]
    public class RsiResult
    {
        public DateTime Timestamp { get; set; }
        public float? Rsi { get; set; }
    }

    /*
     * https://github.com/DaveSkender/Stock.Indicators/tree/main/src/m-r/Rsi
     */
    public class Rsi
    {
        // RELATIVE STRENGTH INDEX
        /// <include file='./info.xml' path='indicator/*' />
        ///
        public static IEnumerable<RsiResult> GetRsi(
            IEnumerable<CandleQuote> quotes,
            int lookbackPeriods = 14)
        {
            // calculate
            return CalcRsi(quotes.ToList(), lookbackPeriods);
        }

        // internals
        private static List<RsiResult> CalcRsi(List<CandleQuote> bdList, int lookbackPeriods)
        {
            // check parameter arguments
            ValidateRsi(lookbackPeriods);

            // initialize
            int length = bdList.Count;
            float? avgGain = 0;
            float? avgLoss = 0;

            List<RsiResult> results = new List<RsiResult>(length);
            float?[] gain = new float?[length]; // gain
            float?[] loss = new float?[length]; // loss
            float? lastValue;

            if (length == 0)
            {
                return results;
            }
            else
            {
                lastValue = bdList[0].Close;
            }

            // roll through quotes
            for (int i = 0; i < bdList.Count; i++)
            {
                var h = bdList[i];
                RsiResult r = new RsiResult
                {
                    Timestamp = h.Timestamp
                };
                results.Add(r);

                gain[i] = (h.Close > lastValue) ? h.Close - lastValue : 0;
                loss[i] = (h.Close < lastValue) ? lastValue - h.Close : 0;
                lastValue = h.Close;

                // calculate RSI
                if (i > lookbackPeriods)
                {
                    avgGain = ((avgGain * (lookbackPeriods - 1)) + gain[i]) / lookbackPeriods;
                    avgLoss = ((avgLoss * (lookbackPeriods - 1)) + loss[i]) / lookbackPeriods;

                    if (avgLoss > 0)
                    {
                        float? rs = avgGain / avgLoss;
                        r.Rsi = 100 - (100 / (1 + rs));
                    }
                    else
                    {
                        r.Rsi = 100;
                    }
                }

                // initialize average gain
                else if (i == lookbackPeriods)
                {
                    float? sumGain = 0;
                    float? sumLoss = 0;

                    for (int p = 1; p <= lookbackPeriods; p++)
                    {
                        sumGain += gain[p];
                        sumLoss += loss[p];
                    }

                    avgGain = sumGain / lookbackPeriods;
                    avgLoss = sumLoss / lookbackPeriods;

                    r.Rsi = (avgLoss > 0) ? 100 - (100 / (1 + (avgGain / avgLoss))) : 100;
                }
            }

            return results;
        }

        // parameter validation
        private static void ValidateRsi(
            int lookbackPeriods)
        {
            // check parameter arguments
            if (lookbackPeriods < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(lookbackPeriods), lookbackPeriods,
                    "Lookback periods must be greater than 0 for RSI.");
            }
        }
    }
}
