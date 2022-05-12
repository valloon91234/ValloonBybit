using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valloon.Stock.Indicators
{
    [Serializable]
    public class ParabolicSarResult
    {
        public DateTime Timestamp { get; set; }
        public float? Sar { get; set; }
        public bool? IsReversal { get; set; }
        public float? OriginalSar { get; set; }
    }

    /**
     * https://github.com/DaveSkender/Stock.Indicators/tree/main/src/m-r/ParabolicSar
     */
    public class ParabolicSar
    {
        // PARABOLIC SAR
        /// <include file='./info.xml' path='indicator/type[@name="Standard"]/*' />
        ///
        public static IEnumerable<ParabolicSarResult> GetParabolicSar(
            IEnumerable<CandleQuote> quotes,
            float accelerationStep = 0.02f,
            float maxAccelerationFactor = 0.2f)
        {
            return GetParabolicSar(
                quotes,
                accelerationStep,
                maxAccelerationFactor,
                accelerationStep);
        }

        /// <include file='./info.xml' path='indicator/type[@name="Extended"]/*' />
        ///
        public static IEnumerable<ParabolicSarResult> GetParabolicSar(
            IEnumerable<CandleQuote> quotes,
            float accelerationStep,
            float maxAccelerationFactor,
            float initialFactor)
        {
            // sort quotes
            List<CandleQuote> quotesList = quotes.ToList();

            // check parameter arguments
            ValidateParabolicSar(
                accelerationStep, maxAccelerationFactor, initialFactor);

            // initialize
            int length = quotesList.Count;
            List<ParabolicSarResult> results = new List<ParabolicSarResult>(length);
            CandleQuote q0;

            if (length == 0)
            {
                return results;
            }
            else
            {
                q0 = quotesList[0];
            }

            float accelerationFactor = initialFactor;
            float extremePoint = q0.High;
            float priorSar = q0.Low;
            bool isRising = true;  // initial guess

            // roll through quotes
            for (int i = 0; i < length; i++)
            {
                CandleQuote q = quotesList[i];

                ParabolicSarResult r = new ParabolicSarResult
                {
                    Timestamp = q.Timestamp
                };
                results.Add(r);

                // skip first one
                if (i == 0)
                {
                    continue;
                }

                // was rising
                if (isRising)
                {
                    float sar =
                        priorSar + (accelerationFactor * (extremePoint - priorSar));

                    // SAR cannot be higher than last two lows
                    if (i >= 2)
                    {
                        float minLastTwo =
                            Math.Min(
                                quotesList[i - 1].Low,
                                quotesList[i - 2].Low);

                        sar = Math.Min(sar, minLastTwo);
                    }

                    // turn down
                    if (q.Low < sar)
                    {
                        r.IsReversal = true;
                        r.Sar = extremePoint;
                        r.OriginalSar = sar;

                        isRising = false;
                        accelerationFactor = initialFactor;
                        extremePoint = q.Low;
                    }

                    // continue rising
                    else
                    {
                        r.IsReversal = false;
                        r.Sar = sar;
                        r.OriginalSar = sar;

                        // new high extreme point
                        if (q.High > extremePoint)
                        {
                            extremePoint = q.High;
                            accelerationFactor =
                                Math.Min(
                                    accelerationFactor + accelerationStep,
                                    maxAccelerationFactor);
                        }
                    }
                }

                // was falling
                else
                {
                    float sar
                        = priorSar - (accelerationFactor * (priorSar - extremePoint));

                    // SAR cannot be lower than last two highs
                    if (i >= 2)
                    {
                        float maxLastTwo = Math.Max(
                            quotesList[i - 1].High,
                            quotesList[i - 2].High);

                        sar = Math.Max(sar, maxLastTwo);
                    }

                    // turn up
                    if (q.High > sar)
                    {
                        r.IsReversal = true;
                        r.Sar = extremePoint;
                        r.OriginalSar = sar;

                        isRising = true;
                        accelerationFactor = initialFactor;
                        extremePoint = q.High;
                    }

                    // continue falling
                    else
                    {
                        r.IsReversal = false;
                        r.Sar = sar;
                        r.OriginalSar = sar;

                        // new low extreme point
                        if (q.Low < extremePoint)
                        {
                            extremePoint = q.Low;
                            accelerationFactor =
                                Math.Min(
                                    accelerationFactor + accelerationStep,
                                    maxAccelerationFactor);
                        }
                    }
                }

                priorSar = (float)r.Sar;
            }

            // remove first trendline since it is an invalid guess
            ParabolicSarResult firstReversal = results
                .Where(x => x.IsReversal == true)
                .OrderBy(x => x.Timestamp)
                .FirstOrDefault();

            int cutIndex = (firstReversal != null)
                ? results.IndexOf(firstReversal)
                : length - 1;

            for (int d = 0; d <= cutIndex; d++)
            {
                ParabolicSarResult r = results[d];
                r.Sar = null;
                r.IsReversal = null;
                r.OriginalSar = null;
            }

            return results;
        }

        // parameter validation
        private static void ValidateParabolicSar(
            float accelerationStep,
            float maxAccelerationFactor,
            float initialFactor)
        {
            // check parameter arguments
            if (accelerationStep <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(accelerationStep), accelerationStep,
                    "Acceleration Step must be greater than 0 for Parabolic SAR.");
            }

            if (maxAccelerationFactor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAccelerationFactor), maxAccelerationFactor,
                    "Max Acceleration Factor must be greater than 0 for Parabolic SAR.");
            }

            if (accelerationStep > maxAccelerationFactor)
            {
                string message = string.Format(
                    "Acceleration Step must be smaller than provided Max Accleration Factor ({0}) for Parabolic SAR.",
                    maxAccelerationFactor);

                throw new ArgumentOutOfRangeException(nameof(accelerationStep), accelerationStep, message);
            }

            if (initialFactor <= 0 || initialFactor >= maxAccelerationFactor)
            {
                throw new ArgumentOutOfRangeException(nameof(initialFactor), initialFactor,
                    "Initial Step must be greater than 0 and less than Max Acceleration Factor for Parabolic SAR.");
            }
        }
    }
}
