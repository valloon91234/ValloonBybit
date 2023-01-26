using Newtonsoft.Json.Linq;
using Skender.Stock.Indicators;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Valloon.Indicators;

namespace Valloon.Trading.Backtest
{
    static class Paxg
    {
        public static void Run()
        {
            //Loader.WriteCSV("PAXGUSDT", 1, new DateTime(2023, 1, 20, 14, 24, 0, DateTimeKind.Utc)); return;
            //Loader.WriteCSV("BTCUSDT", 1, new DateTime(2020, 3, 25, 8, 0, 0, DateTimeKind.Utc)); return;

            Benchmark(5);

            //ThreadPool.QueueUserWorkItem(state => runHCS(new DateTime(2021, 7, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        static double Benchmark(int step)
        {
            var filename = $"data-PAXGUSDT-1.csv";

            DateTime startTime = new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime? endTime = null;// new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var totalCount = Loader.ReadCSV(filename, out var quoteList, out var quoteDList);
            int totalDays = (int)((endTime ?? quoteList.Last().Date) - startTime).TotalDays;
            Logger logger = new Logger($"{DateTime.Now:yyyyMMddTHHmmss}    step = {step}    {startTime:yyyyMMdd} ~ {endTime:yyyyMMdd} ({totalDays:N0}) days");
            logger.WriteLine("\n" + logger.LogFilename + "\n");
            logger.WriteLine($"{totalCount} loaded. ({totalDays:N0} days)");
            Console.Title = logger.LogFilename;

            var list = new List<QuoteD>(quoteDList);
            int basePrice = (((int)list[0].Open) / step + 1) * step;
            int lastBasePrice = basePrice;
            list.RemoveAll(x => x.Date < startTime || endTime != null && x.Date > endTime.Value);
            int count = list.Count;
            int profitCount = 0, lastProfitcount = 0;
            for (int i = 0; i < count; i++)
            {
                var candle = list[i];
                if (candle.Open > candle.Close)
                {
                    if ((int)candle.High - basePrice >= step)
                    {
                        profitCount += ((int)candle.High) / step - basePrice / step;
                        basePrice = ((int)candle.High) / step * step;
                    }
                    basePrice = Math.Min(basePrice, (((int)candle.Low) / step + 1) * step);
                    if ((int)candle.Close - basePrice >= step)
                    {
                        profitCount += ((int)candle.Close) / step - basePrice / step;
                        basePrice = ((int)candle.Close) / step * step;
                    }
                    basePrice = Math.Min(basePrice, (((int)candle.Close) / step + 1) * step);
                }
                else
                {
                    basePrice = Math.Min(basePrice, (((int)candle.Low) / step + 1) * step);
                    if ((int)candle.High - basePrice >= step)
                    {
                        profitCount += ((int)candle.High) / step - basePrice / step;
                        basePrice = ((int)candle.High) / step * step;
                    }
                    basePrice = Math.Min(basePrice, (((int)candle.Close) / step + 1) * step);
                }
                if (profitCount > lastProfitcount)
                    logger.WriteLine($"{candle.Date:yyyy-MM-dd HH:mm:ss} \t {candle.Open} / {candle.High} / {candle.Low} / {candle.Close} \t base = {basePrice} \t profit = {profitCount}", ConsoleColor.Green);
                else if (lastBasePrice != basePrice)
                    logger.WriteLine($"{candle.Date:yyyy-MM-dd HH:mm:ss} \t {candle.Open} / {candle.High} / {candle.Low} / {candle.Close} \t base = {basePrice}");
                lastBasePrice = basePrice;
                lastProfitcount = profitCount;
            }

            double profit = step / 2000d * step / 10 * profitCount;
            string result = $"{startTime} ~ {endTime} ({totalDays} days) \t count = {profitCount} \t profit = {profit}";
            logger.WriteLine($"\r\n{result}");
            return profit;
        }

    }

}
