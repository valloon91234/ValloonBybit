using IO.Swagger.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valloon.Trading
{
    /**
     * https://nullbeans.com/how-to-calculate-the-relative-strength-index-rsi/
     * https://docs.google.com/spreadsheets/d/1oOb7R6MkF5DlgKSlHlB19T3vEFA3CcWUyCCTGSInbO4/edit?usp=sharing
     */
    public class RSI
    {
        private static double CalcSmmaUp(KlineRes[] candlesticks, int n, int i, double avgUt1)
        {
            if (avgUt1 == 0)
            {
                double sumUpChanges = 0;
                for (int j = 0; j < n; j++)
                {
                    double change = (double)candlesticks[i - j].Close.Value - (double)candlesticks[i - j].Open.Value;
                    if (change > 0)
                    {
                        sumUpChanges += change;
                    }
                }
                return sumUpChanges / n;
            }
            else
            {
                double change = (double)candlesticks[i].Close.Value - (double)candlesticks[i].Open.Value;
                if (change < 0)
                {
                    change = 0;
                }
                return ((avgUt1 * (n - 1)) + change) / n;
            }
        }

        private static double CalcSmmaDown(KlineRes[] candlesticks, int n, int i, double avgDt1)
        {
            if (avgDt1 == 0)
            {
                double sumDownChanges = 0;
                for (int j = 0; j < n; j++)
                {
                    double change = (double)candlesticks[i - j].Close.Value - (double)candlesticks[i - j].Open.Value;
                    if (change < 0)
                    {
                        sumDownChanges -= change;
                    }
                }
                return sumDownChanges / n;
            }
            else
            {
                double change = (double)candlesticks[i].Close.Value - (double)candlesticks[i].Open.Value;
                if (change > 0)
                {
                    change = 0;
                }
                return ((avgDt1 * (n - 1)) - change) / n;
            }
        }

        public static double[] CalculateRSIValues(KlineRes[] candlesticks, int n)
        {
            int length = candlesticks.Length;
            double[] results = new double[length];
            double ut1 = 0;
            double dt1 = 0;
            for (int i = 0; i < length; i++)
            {
                if (i < n)
                {
                    continue;
                }
                ut1 = CalcSmmaUp(candlesticks, n, i, ut1);
                dt1 = CalcSmmaDown(candlesticks, n, i, dt1);
                results[i] = 100d - 100d / (1d + ut1 / dt1);
            }
            return results;
        }
    }
}
