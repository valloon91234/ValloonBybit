using Newtonsoft.Json.Linq;
using Skender.Stock.Indicators;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using Valloon.Indicators;

namespace Valloon.Trading.Backtest
{
    static class Lbma
    {
        public static void Run()
        {
            //Loader.WriteCSV("PAXGUSDT", 1, new DateTime(2022, 12, 25, 13, 43, 0, DateTimeKind.Utc)); return;
            //Loader.WriteCSV("BTCUSDT", 1, new DateTime(2020, 3, 25, 8, 0, 0, DateTimeKind.Utc)); return;

            //TestPaxg(55);
            TestBTC(55);

            //ThreadPool.QueueUserWorkItem(state => runHCS(new DateTime(2021, 7, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        static string TestPaxg(int durationMinutes)
        {
            var filename = $"data-PAXGUSDT-1.csv";

            DateTime startTime = new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime? endTime = null;// new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var totalCount = Loader.ReadCSV(filename, out var quoteList, out var quoteDList);
            int totalDays = (int)((endTime ?? quoteList.Last().Date) - startTime).TotalDays;
            //Logger logger = new Logger($"{DateTime.Now:yyyyMMddTHHmmss}    {startTime:yyyyMMdd} ~ {endTime:yyyyMMdd} ({totalDays:N0}) days");
            //logger.WriteLine("\n" + logger.LogFilename + "\n");
            //logger.WriteLine($"{totalCount} loaded. ({totalDays:N0} days)");
            //Console.Title = logger.LogFilename;

            var list = new List<QuoteD>(quoteDList);
            list.RemoveAll(x => x.Date < startTime || endTime != null && x.Date > endTime.Value);
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                var utcTime = list[i].Date;
                TimeZoneInfo cetTimezone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                var londonTime = utcTime;
                //var londonTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, gmtTimezone);
                if (cetTimezone.IsDaylightSavingTime(londonTime))
                    londonTime = londonTime.AddHours(1);
                //if (londonTime.Hour == 10 && londonTime.Minute == 30)
                if (londonTime.Hour == 15 && londonTime.Minute == 0)
                {
                    var openPrice = list[i].Open;
                    var lowPrice = list[i].Low;
                    var highPrice = list[i].High;
                    for (int j = 0; j < durationMinutes; j++)
                    {
                        i++;
                        if (lowPrice > list[i].Low) lowPrice = list[i].Low;
                        if (highPrice < list[i].High) highPrice = list[i].High;
                    }
                    var bull = highPrice / openPrice - 1;
                    var bear = 1 - lowPrice / openPrice;
                    Logger.Write($"{utcTime:yyyy-MM-dd HH:mm:ss} \t ", ConsoleColor.White);
                    if (cetTimezone.IsDaylightSavingTime(utcTime))
                        Logger.Write($"{londonTime:yyyy-MM-dd HH:mm:ss} \t ", ConsoleColor.DarkYellow);
                    else
                        Logger.Write($"{londonTime:yyyy-MM-dd HH:mm:ss} \t ", ConsoleColor.White);
                    if (londonTime.DayOfWeek == DayOfWeek.Saturday || londonTime.DayOfWeek == DayOfWeek.Sunday)
                        Logger.Write($"{londonTime.DayOfWeek} \t\t ", ConsoleColor.Red);
                    else
                        Logger.Write($"{londonTime.DayOfWeek} \t\t ", ConsoleColor.White);

                    Logger.Write($"{openPrice} / {highPrice} / {lowPrice} \t ", ConsoleColor.DarkGray);


                    if (bull < 0.0025)
                        Logger.Write($"{bull:F6} \t ", ConsoleColor.DarkGray);
                    else if (bull >= 0.005)
                        Logger.Write($"{bull:F6} \t ", ConsoleColor.Green);
                    else
                        Logger.Write($"{bull:F6} \t ", ConsoleColor.White);
                    if (bear < 0.0025)
                        Logger.Write($"{bear:F6} \t ", ConsoleColor.DarkGray);
                    else if (bear >= 0.005)
                        Logger.Write($"{bear:F6} \t ", ConsoleColor.Red);
                    else
                        Logger.Write($"{bear:F6} \t ", ConsoleColor.White);
                    Logger.WriteLine();
                }
            }

            return null;
        }

        static string TestBTC(int durationMinutes)
        {
            var filename = $"data-BTCUSDT-1.csv";

            DateTime startTime = new DateTime(2020, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime? endTime = null;// new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var totalCount = Loader.ReadCSV(filename, out var quoteList, out var quoteDList);
            int totalDays = (int)((endTime ?? quoteList.Last().Date) - startTime).TotalDays;
            //Logger logger = new Logger($"{DateTime.Now:yyyyMMddTHHmmss}    {startTime:yyyyMMdd} ~ {endTime:yyyyMMdd} ({totalDays:N0}) days");
            //logger.WriteLine("\n" + logger.LogFilename + "\n");
            //logger.WriteLine($"{totalCount} loaded. ({totalDays:N0} days)");
            //Console.Title = logger.LogFilename;

            var list = new List<QuoteD>(quoteDList);
            list.RemoveAll(x => x.Date < startTime || endTime != null && x.Date > endTime.Value);
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                var utcTime = list[i].Date;
                TimeZoneInfo cetTimezone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                var londonTime = utcTime;
                //var londonTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, gmtTimezone);
                if (cetTimezone.IsDaylightSavingTime(londonTime))
                    londonTime = londonTime.AddHours(1);
                //if (londonTime.Hour == 10 && londonTime.Minute == 30)
                if (londonTime.Hour == 15 && londonTime.Minute == 0)
                {
                    var openPrice = list[i].Open;
                    var lowPrice = list[i].Low;
                    var highPrice = list[i].High;
                    for (int j = 0; j < durationMinutes; j++)
                    {
                        i++;
                        if (lowPrice > list[i].Low) lowPrice = list[i].Low;
                        if (highPrice < list[i].High) highPrice = list[i].High;
                    }
                    var bull = highPrice / openPrice - 1;
                    var bear = 1 - lowPrice / openPrice;
                    Logger.Write($"{utcTime:yyyy-MM-dd HH:mm:ss} \t ", ConsoleColor.White);
                    if (cetTimezone.IsDaylightSavingTime(utcTime))
                        Logger.Write($"{londonTime:HH:mm:ss} \t ", ConsoleColor.DarkYellow);
                    else
                        Logger.Write($"{londonTime:HH:mm:ss} \t ", ConsoleColor.White);
                    if (londonTime.DayOfWeek == DayOfWeek.Saturday || londonTime.DayOfWeek == DayOfWeek.Sunday)
                        Logger.Write($"{londonTime.DayOfWeek} \t\t ", ConsoleColor.Red);
                    else
                        Logger.Write($"{londonTime.DayOfWeek} \t\t ", ConsoleColor.White);

                    Logger.Write($"{openPrice:F1} / {highPrice:F1} / {lowPrice:F1} \t\t ", ConsoleColor.Gray);


                    if (bull <= 0.001)
                        Logger.Write($"{bull:F4} \t ", ConsoleColor.DarkGray);
                    else if (bull >= 0.005)
                        Logger.Write($"{bull:F4} \t ", ConsoleColor.Green);
                    else
                        Logger.Write($"{bull:F4} \t ", ConsoleColor.White);
                    if (bear <= 0.001)
                        Logger.Write($"{bear:F4} \t ", ConsoleColor.DarkGray);
                    else if (bear >= 0.005)
                        Logger.Write($"{bear:F4} \t ", ConsoleColor.Red);
                    else
                        Logger.Write($"{bear:F4} \t ", ConsoleColor.White);
                    Logger.WriteLine();
                }
            }

            return null;
        }

    }

}
