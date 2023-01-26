using IO.Swagger.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Valloon.Indicators;
using Valloon.Trading;
using Valloon.Utils;

namespace Valloon.Trading.Backtest
{
    static class Loader
    {
        public static void WriteCSV(string symbol, int interval, DateTime startTime, DateTime? endTime = null)
        {
            BybitLinearApiHelper apiHelper = new BybitLinearApiHelper();
            if (endTime == null) endTime = BybitLinearApiHelper.ServerTime;
            //string filename = $"data-{symbol}-{interval}  {startTime:yyyy-MM-dd} ~ {endTime:yyyy-MM-dd}.csv";
            string filename = $"data-{symbol}-{interval}--{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            File.Delete(filename);
            using (var writer = new StreamWriter(filename, false, Encoding.UTF8))
            {
                writer.WriteLine($"timestamp,date,time,open,high,low,close,volume");
                while (true)
                {
                    try
                    {
                        if (startTime > endTime.Value)
                        {
                            Console.WriteLine($"end: nextTime = {startTime:yyyy-MM-dd HH:mm:ss} > {endTime.Value:yyyy-MM-dd HH:mm:ss}");
                            break;
                        }
                        var list = apiHelper.GetCandleList(symbol, interval.ToString(), startTime, 181);
                        int count = list.Count;
                        for (int i = 0; i < count - 1; i++)
                        {
                            var t = list[i];
                            try
                            {
                                writer.WriteLine($"{t.Timestamp():yyyy-MM-dd HH:mm:ss},{t.Timestamp():yyyy-MM-dd},{t.Timestamp():HH:mm},{t.Open},{t.High},{t.Low},{t.Close},{t.Volume.Value}");
                                writer.Flush();
                            }
                            catch (Exception ex)
                            {
                                if (ex.Message.ContainsIgnoreCase("UNIQUE constraint failed:"))
                                    Console.WriteLine($"Failed: {t.OpenTime:yyyy-MM-dd HH:mm:ss} - Already exists.");
                                else
                                    Console.WriteLine($"Failed: {t.OpenTime:yyyy-MM-dd HH:mm:ss}\r\n{ex.StackTrace}");
                            }
                        }
                        Console.WriteLine($"Inserted: {startTime:yyyy-MM-dd HH:mm:ss}");
                        startTime = startTime.AddMinutes(interval * 180);
                        Thread.Sleep(500);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception: {ex.Message}");
                        Thread.Sleep(5000);
                    }
                }
            }
        }

        //public static List<CandleQuote> ReadCSV(string filename, int x)
        //{
        //    var lines = File.ReadAllLines(filename);
        //    int lineCount = lines.Length;
        //    var result = new List<CandleQuote>();
        //    for (int i = 1; i < lineCount; i++)
        //    {
        //        var line = lines[i];
        //        var values = line.Split(',');
        //        result.Add(new CandleQuote
        //        {
        //            Timestamp = DateTime.ParseExact(values[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
        //            Open = (int)(decimal.Parse(values[3]) * x),
        //            High = (int)(decimal.Parse(values[4]) * x),
        //            Low = (int)(decimal.Parse(values[5]) * x),
        //            Close = (int)(decimal.Parse(values[6]) * x),
        //            Volume = (int)(decimal.Parse(values[7])),
        //        });
        //    }
        //    return result;
        //}

        public static int ReadCSV(string filename, out List<Skender.Stock.Indicators.Quote> quoteList, out List<QuoteD> quoteDList)
        {
            var lines = File.ReadAllLines(filename);
            int lineCount = lines.Length;
            quoteList = new List<Skender.Stock.Indicators.Quote>();
            quoteDList = new List<QuoteD>();
            int count = 0;
            for (int i = 1; i < lineCount; i++)
            {
                var line = lines[i];
                var values = line.Split(',');
                quoteList.Add(new Skender.Stock.Indicators.Quote
                {
                    Date = DateTime.ParseExact(values[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
                    Open = decimal.Parse(values[3]),
                    High = decimal.Parse(values[4]),
                    Low = decimal.Parse(values[5]),
                    Close = decimal.Parse(values[6]),
                    Volume = decimal.Parse(values[7]),
                });
                quoteDList.Add(new QuoteD
                {
                    Date = DateTime.ParseExact(values[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
                    Open = double.Parse(values[3]),
                    High = double.Parse(values[4]),
                    Low = double.Parse(values[5]),
                    Close = double.Parse(values[6]),
                    Volume = double.Parse(values[7]),
                });
                count++;
            }
            return count;
        }

        public static List<CandleQuote> LoadBinListFrom1m(int size, List<CandleQuote> list)
        {
            if (size == 1) return list;
            int count = list.Count;
            var resultList = new List<CandleQuote>();
            int i = 0;
            while (i < count)
            {
                if (list[i].Timestamp.Minute % size != 0)
                {
                    i++;
                    continue;
                }
                DateTime timestamp = list[i].Timestamp;
                int open = list[i].Open;
                int high = list[i].High;
                int low = list[i].Low;
                int close = list[i].Close;
                long volume = list[i].Volume;
                for (int j = i + 1; j < i + size && j < count; j++)
                {
                    if (high < list[j].High) high = list[j].High;
                    if (low > list[j].Low) low = list[j].Low;
                    close = list[j].Close;
                    volume += list[j].Volume;
                }
                resultList.Add(new CandleQuote
                {
                    Timestamp = timestamp,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume
                });
                i += size;
            }
            return resultList;
        }

        //public static List<CandleQuote> LoadBinListFrom5m(string binSize, List<CandleQuote> list)
        //{
        //    int batchLength;
        //    switch (binSize)
        //    {
        //        case "15m":
        //            batchLength = 3;
        //            break;
        //        case "30m":
        //            batchLength = 6;
        //            break;
        //        default:
        //            throw new Exception($"Invalid bin_size: {binSize}");
        //    }
        //    int count = list.Count;
        //    var resultList = new List<CandleQuote>();
        //    int i = 0;
        //    while (i < count)
        //    {
        //        if (list[i].Timestamp.Minute % (batchLength * 5) != 5)
        //        {
        //            i++;
        //            continue;
        //        }
        //        DateTime timestamp = list[i].Timestamp.AddMinutes(25);
        //        int open = list[i].Open;
        //        int high = list[i].High;
        //        int low = list[i].Low;
        //        int close = list[i].Close;
        //        int volume = list[i].Volume;
        //        for (int j = i + 1; j < i + batchLength && j < count; j++)
        //        {
        //            if (high < list[j].High) high = list[j].High;
        //            if (low > list[j].Low) low = list[j].Low;
        //            close = list[j].Close;
        //            volume += list[j].Volume;
        //        }
        //        resultList.Add(new CandleQuote
        //        {
        //            Timestamp = timestamp,
        //            Open = open,
        //            High = high,
        //            Low = low,
        //            Close = close,
        //            Volume = volume
        //        });
        //        i += batchLength;
        //    }
        //    return resultList;
        //}

    }
}
