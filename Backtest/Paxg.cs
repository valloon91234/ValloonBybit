using System;
using System.Collections.Generic;
using System.Linq;
using Valloon.Indicators;

namespace Valloon.Trading.Backtest
{
    static class Paxg
    {
        public static void Run()
        {
            //Loader.WriteCSV("PAXGUSDT", 1, new DateTime(2023, 5, 8, 8, 0, 0, DateTimeKind.Utc)); return;

            Benchmark(5, 0);

            //ThreadPool.QueueUserWorkItem(state => runHCS(new DateTime(2021, 7, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        static double Benchmark(int heightOpen, int heightClose)
        {
            if (heightClose == 0) heightClose = heightOpen;

            var filename = $"data-PAXGUSDT-1.csv";

            DateTime startTime = new DateTime(2022, 12, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime? endTime = null;// new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var totalCount = Loader.ReadCSV(filename, out var quoteList, out var quoteDList);
            int totalDays = (int)((endTime ?? quoteList.Last().Date) - startTime).TotalDays;
            Logger logger = new Logger($"{DateTime.Now:yyyyMMddTHHmmss}    step = {heightOpen} - {heightClose}    {startTime:yyyyMMdd} ~ {endTime:yyyyMMdd} ({totalDays:N0}) days");
            logger.WriteLine("\n" + logger.LogFilename + "\n");
            logger.WriteLine($"{totalCount} loaded. ({totalDays:N0} days)");
            Console.Title = logger.LogFilename;

            var list = new List<QuoteD>(quoteDList);
            int basePrice = (((int)list[0].Open) / heightOpen + 1) * heightOpen;
            int lastBasePrice = basePrice;
            list.RemoveAll(x => x.Date < startTime || endTime != null && x.Date > endTime.Value);
            int count = list.Count;
            int profitCount = 0, lastProfitcount = 0;
            for (int i = 0; i < count; i++)
            {
                var candle = list[i];
                if (candle.Open > candle.Close)
                {
                    if ((int)candle.High - basePrice > heightClose)
                    {
                        profitCount += ((int)candle.High - basePrice) / heightClose;
                        basePrice = ((int)candle.High) / heightOpen * heightOpen - heightClose + heightOpen;
                    }
                    basePrice = Math.Min(basePrice, (((int)candle.Low) / heightOpen + 1) * heightOpen);
                    if ((int)candle.Close - basePrice > heightClose)
                    {
                        profitCount += ((int)candle.Close - basePrice) / heightClose;
                        basePrice = ((int)candle.Close) / heightOpen * heightOpen - heightClose + heightOpen;
                    }
                    basePrice = Math.Min(basePrice, (((int)candle.Close) / heightOpen + 1) * heightOpen);
                }
                else
                {
                    basePrice = Math.Min(basePrice, (((int)candle.Low) / heightOpen + 1) * heightOpen);
                    if ((int)candle.High - basePrice > heightClose)
                    {
                        profitCount += ((int)candle.High - basePrice) / heightClose;
                        basePrice = ((int)candle.High) / heightOpen * heightOpen - heightClose + heightOpen;
                    }
                    basePrice = Math.Min(basePrice, (((int)candle.Close) / heightOpen + 1) * heightOpen);
                }
                if (profitCount > lastProfitcount)
                    logger.WriteLine($"{candle.Date:yyyy-MM-dd HH:mm:ss} \t {candle.Open} / {candle.High} / {candle.Low} / {candle.Close} \t base = {basePrice} \t profit = {profitCount}", ConsoleColor.Green);
                else if (lastBasePrice != basePrice)
                    logger.WriteLine($"{candle.Date:yyyy-MM-dd HH:mm:ss} \t {candle.Open} / {candle.High} / {candle.Low} / {candle.Close} \t base = {basePrice}");
                lastBasePrice = basePrice;
                lastProfitcount = profitCount;
            }

            double profitRate = heightClose / 2000d * heightOpen / 125 * 7.5 * profitCount;
            string result = $"{startTime} ~ {endTime} ({totalDays} days) \t profitCount = {profitCount} \t profitRate = {profitRate * 100} %";
            logger.WriteLine($"\r\n{result}");
            return profitRate;
        }

    }

}
