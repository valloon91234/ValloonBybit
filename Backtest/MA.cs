using Newtonsoft.Json.Linq;
using Skender.Stock.Indicators;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Valloon.Indicators;

namespace Valloon.Trading.Backtest
{
    static class MA
    {
        public static void Run()
        {
            //Loader.WriteCSV("XRPUSDT", 5, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc)); return;

            Benchmark("SOL", 1, false);
            Benchmark("SOL", 1, true);
            Benchmark("SOL", 2, false);
            Benchmark("SOL", 2, true);
            Benchmark("ETH", 1, false);
            Benchmark("ETH", 1, true);
            Benchmark("ETH", 2, false);
            Benchmark("ETH", 2, true);
            Benchmark("XRP", 1, false);
            Benchmark("XRP", 1, true);
            Benchmark("XRP", 2, false);
            Benchmark("XRP", 2, true);
            //ThreadPool.QueueUserWorkItem(state => runHCS(new DateTime(2021, 7, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        static double Benchmark(string symbol, int buyOrSell, bool useEMA)
        {
            const double makerFee = 0.0003f;
            const double takerFee = 0.002f;
            var filename = $"data-{symbol}USDT-5.csv";

            DateTime startTime = new DateTime(2022, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime? endTime = null;// new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var totalCount = Loader.ReadCSV(filename, out var quoteList, out var quoteDList);
            int totalDays = (int)((endTime ?? quoteList.Last().Date) - startTime).TotalDays;
            Logger logger = new Logger($"{DateTime.Now:yyyyMMddTHHmmss}    {symbol}    {(useEMA ? "EMA" : "SMA")}    BS = {buyOrSell}    {startTime:yyyyMMdd} ~ {endTime:yyyyMMdd} ({totalDays:N0}) days");
            logger.WriteLine("\n" + logger.LogFilename + "\n");
            logger.WriteLine($"{totalCount} loaded. ({totalDays:N0} days)");
            Console.Title = logger.LogFilename;

            List<Dictionary<string, double>> topList = new List<Dictionary<string, double>>();
            //var maLengthArray = new int[] { 15, 20, 30, 45, 60, 90, 120, 150, 180, 240, 300, 360, 420, 480, 540, 600, 660, 720, 900, 1080, 1440 };
            //var maLengthArray = new int[] { 420, 480, 540, 600, 660, 720 };
            var maLengthArray = new int[] { 6, 9, 12, 18, 24, 30, 36, 48, 60, 72, 84, 96, 108, 120, 132, 144, 156, 168, 180, 216, 252, 288, 360, 432, 504, 576, 648, 720 };
            //var maLengthArray = new int[] { 96, 108, 120, 132, 144 };
            foreach (int maLength in maLengthArray)
            //int smaLength1 = 144;
            {
                var list = new List<Quote>(quoteList);
                var listD = new List<QuoteD>(quoteDList);
                var smaList = list.GetSma(maLength).ToList();
                var emaList = list.GetEma(maLength).ToList();
                list.RemoveAll(x => x.Date < startTime || endTime != null && x.Date > endTime.Value);
                listD.RemoveAll(x => x.Date < startTime || endTime != null && x.Date > endTime.Value);
                smaList.RemoveAll(x => x.Date < startTime || endTime != null && x.Date > endTime.Value);
                emaList.RemoveAll(x => x.Date < startTime || endTime != null && x.Date > endTime.Value);
                int count = list.Count;

                for (int maDelay = 1; maDelay <= 12; maDelay++)
                //int delay = 1;
                {
                    for (double limitX = 0.01; limitX < 0.16; limitX += .01)
                    //for (double limitX = 0.08; limitX < 0.11; limitX += .01)
                    //double limitX = 0.02;
                    {
                        for (double stopX = 0.01f; stopX < 0.06; stopX += .005)
                        //double stopX = 0.01;
                        {
                            for (double closeX = 0.01f; closeX < 0.11; closeX += .005)
                            //double closeX = 0.01;
                            {
                                int tryCount = 0;
                                int succeedCount = 0, failedCount = 0;
                                double finalPercent = 1, finalPercent2 = 1;
                                int position = 0;
                                double positionEntryPrice = 0;
                                double leverage = Math.Min(0.1 / stopX, 20);
                                for (int i = maDelay; i < count - 1; i++)
                                {
                                    if (position == 0)
                                    {
                                        var ma = useEMA ? emaList[i - maDelay].Ema.Value : smaList[i - maDelay].Sma.Value;
                                        if (buyOrSell == 1)
                                        {
                                            var limitPrice = ma * (1 - limitX);
                                            if (listD[i].Open > limitPrice && listD[i].Low < limitPrice)
                                            {
                                                tryCount++;
                                                var stopPrice = limitPrice * (1 - stopX);
                                                var closePrice = limitPrice * (1 + closeX);
                                                if (listD[i].Low < stopPrice)
                                                {
                                                    failedCount++;
                                                    finalPercent *= 1 - stopX - takerFee;
                                                    finalPercent2 *= 1 - (stopX + takerFee) * leverage;
                                                }
                                                else if (listD[i].Close > closePrice)
                                                {
                                                    succeedCount++;
                                                    finalPercent *= 1 + closeX - makerFee;
                                                    finalPercent2 *= 1 + (closeX - makerFee) * leverage;
                                                }
                                                else
                                                {
                                                    position = 1;
                                                    positionEntryPrice = limitPrice;
                                                }
                                            }
                                        }
                                        else if (buyOrSell == 2)
                                        {
                                            var limitPrice = ma * (1 + limitX);
                                            if (listD[i].Open < limitPrice && listD[i].High > limitPrice)
                                            {
                                                tryCount++;
                                                var stopPrice = limitPrice * (1 + stopX);
                                                var closePrice = limitPrice * (1 - closeX);
                                                if (listD[i].High > stopPrice)
                                                {
                                                    failedCount++;
                                                    finalPercent *= 1 - stopX - takerFee;
                                                    finalPercent2 *= 1 - (stopX + takerFee) * leverage;
                                                }
                                                else if (listD[i].Close < closePrice)
                                                {
                                                    succeedCount++;
                                                    finalPercent *= 1 + closeX - makerFee;
                                                    finalPercent2 *= 1 + (closeX - makerFee) * leverage;
                                                }
                                                else
                                                {
                                                    position = -1;
                                                    positionEntryPrice = limitPrice;
                                                }
                                            }
                                        }
                                    }
                                    else if (position == 1)
                                    {
                                        var stopPrice = positionEntryPrice * (1 - stopX);
                                        var closePrice = positionEntryPrice * (1 + closeX);
                                        if (listD[i].Low < stopPrice)
                                        {
                                            failedCount++;
                                            finalPercent *= 1 - stopX - takerFee;
                                            finalPercent2 *= 1 - (stopX + takerFee) * leverage;
                                            position = 0;
                                            positionEntryPrice = 0;
                                        }
                                        else if (listD[i].High > closePrice)
                                        {
                                            succeedCount++;
                                            finalPercent *= 1 + closeX - makerFee;
                                            finalPercent2 *= 1 + (closeX - makerFee) * leverage;
                                            position = 0;
                                            positionEntryPrice = 0;
                                        }
                                    }
                                    else if (position == -1)
                                    {
                                        var stopPrice = positionEntryPrice * (1 + stopX);
                                        var closePrice = positionEntryPrice * (1 - closeX);
                                        if (listD[i].High > stopPrice)
                                        {
                                            failedCount++;
                                            finalPercent *= 1 - stopX - takerFee;
                                            finalPercent2 *= 1 - (stopX + takerFee) * leverage;
                                            position = 0;
                                            positionEntryPrice = 0;
                                        }
                                        else if (listD[i].Low < closePrice)
                                        {
                                            succeedCount++;
                                            finalPercent *= 1 + closeX - makerFee;
                                            finalPercent2 *= 1 + (closeX - makerFee) * leverage;
                                            position = 0;
                                            positionEntryPrice = 0;
                                        }
                                    }
                                }
                                double successRate = failedCount > 0 ? (double)succeedCount / failedCount : succeedCount;
                                if (finalPercent > 1f && succeedCount > 0)
                                {
                                    Dictionary<string, double> dic = new Dictionary<string, double>
                                    {
                                        { "makerFee", makerFee },
                                        { "takerFee", takerFee },
                                        { "maLength", maLength },
                                        { "maDelay", maDelay },
                                        { "limitX", limitX },
                                        { "closeX", closeX },
                                        { "stopX", stopX },
                                        { "tryCount", tryCount },
                                        { "succeedCount", succeedCount },
                                        { "failedCount", failedCount },
                                        { "leverage", leverage },
                                        { "finalPercent", finalPercent },
                                        { "finalPercent2", finalPercent2 },
                                    };
                                    int topListCount = topList.Count;
                                    if (topListCount > 0)
                                    {
                                        while (topListCount > 10000)
                                        {
                                            topList.RemoveAt(0);
                                            topListCount--;
                                        }
                                        for (int i = 0; i < topListCount; i++)
                                        {
                                            if (topList[i]["finalPercent2"] > finalPercent2)
                                            {
                                                topList.Insert(i, dic);
                                                goto topListEnd;
                                            }
                                        }
                                        topList.Add(dic);
                                    topListEnd:;
                                    }
                                    else
                                    {
                                        topList.Add(dic);
                                    }
                                    logger.WriteLine($"sma = {maLength} / {maDelay} \t limit = {limitX:F4} / {closeX:F4} / {stopX:F4} \t count = {tryCount} / {succeedCount} / {failedCount} / {successRate:F2} \t % = {finalPercent:F4} / {finalPercent2:F4} (x{leverage:F2})");
                                }
                                else
                                {
                                    logger.WriteLine($"sma = {maLength} / {maDelay} \t limit = {limitX:F4} / {closeX:F4} / {stopX:F4} \t count = {tryCount} / {succeedCount} / {failedCount} / {successRate:F2} \t % = {finalPercent:F4} / {finalPercent2:F4} (x{leverage:F2})", ConsoleColor.DarkGray, false);
                                }
                                if (tryCount == 0)
                                    goto next_limit;
                            }
                        }
                    next_limit:;
                    }
                }
            }
            logger.WriteLine($"\r\n\r\n\r\n\r\ntopList={topList.Count}\r\n" + JArray.FromObject(topList).ToString());
            if (topList.Count > 0)
                return topList[topList.Count - 1]["finalPercent"];
            return 0;
        }

        static string Test()
        {
            //TestSAR3(new DateTime(2022, 3, 16, 0, 0, 0, DateTimeKind.Utc)); return null;

            string result = "";
            result += Test(new DateTime(2022, 4, 14, 0, 0, 0, DateTimeKind.Utc)) + "\r\n\r\n";
            result += Test(new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc)) + "\r\n\r\n";
            result += Test(new DateTime(2022, 3, 23, 0, 0, 0, DateTimeKind.Utc), new DateTime(2022, 4, 14, 0, 0, 0, DateTimeKind.Utc)) + "\r\n\r\n";
            result += Test(new DateTime(2022, 3, 16, 0, 0, 0, DateTimeKind.Utc), new DateTime(2022, 4, 14, 0, 0, 0, DateTimeKind.Utc)) + "\r\n\r\n";
            result += Test(new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc)) + "\r\n\r\n";
            result += Test(new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2022, 3, 15, 0, 0, 0, DateTimeKind.Utc)) + "\r\n\r\n";
            result += Test(new DateTime(2022, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc)) + "\r\n\r\n";
            result += Test(new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2022, 2, 1, 0, 0, 0, DateTimeKind.Utc)) + "\r\n\r\n";
            result += Test(new DateTime(2021, 12, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc)) + "\r\n\r\n";
            result += Test(new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 12, 1, 0, 0, 0, DateTimeKind.Utc)) + "\r\n";
            Console.WriteLine("\r\n\r\n================================\r\n");
            Console.WriteLine(result);
            return result;
        }

        static string Test(DateTime startTime, DateTime? endTime = null)
        {
            //const double makerFee = 0.0003f;
            //const double takerFee = 0.002f;
            const double makerFee = 0.001f;
            const double takerFee = 0.002f;
            const int binSize1 = 5;
            //const int binSize2 = 60;
            double start1 = 0.0015f;
            double step1 = 0.0015f;
            double max1 = 0.03f;
            //double start2 = 0.00044f;
            //double step2 = start2;
            //double max2 = 0.017f;
            double closeLimit = 0.054f;
            double stopLoss = 0.012f;

            //DateTime startTime = new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc);
            //DateTime? endTime = null;
            var list1m = new List<CandleQuote>();// Dao.SelectAll(SYMBOL, "1");
                                                 //var list1m = Dao.SelectAll(SYMBOL, "1m");
            var list1 = Loader.LoadBinListFrom1m(binSize1, list1m);
            //List<SolBin> list2 = LoadBinListFrom1m(binSize2, list1m);
            int count1 = list1.Count;
            int totalDays = (int)(list1.Last().Timestamp - startTime).TotalDays;
            Logger logger = new Logger($"{DateTime.Now:yyyy-MM-dd  HH.mm.ss}    TestSAR2    bin = {binSize1}    takerFee = {takerFee}    ({startTime:yyyy-MM-dd HH.mm.ss} ~ {totalDays:N0} days)");
            logger.WriteLine("\n" + logger.LogFilename + "\n");
            logger.WriteLine($"{count1} loaded. ({totalDays:N0} days)");
            Console.Title = logger.LogFilename;

            var quoteList1 = new List<CandleQuote>();
            foreach (var t in list1)
            {
                quoteList1.Add(new CandleQuote
                {
                    Timestamp = t.Timestamp,
                    Open = t.Open,
                    High = t.High,
                    Low = t.Low,
                    Close = t.Close,
                    Volume = t.Volume,
                });
            }
            //var quoteList2 = new List<ParabolicSarQuote>();
            //foreach (var t in list2)
            //{
            //    quoteList2.Add(new ParabolicSarQuote
            //    {
            //        Date = t.Timestamp,
            //        Open = t.Open,
            //        High = t.High,
            //        Low = t.Low,
            //        Close = t.Close,
            //        Volume = t.Volume,
            //    });
            //}
            list1.RemoveAll(x => x.Timestamp < startTime || endTime != null && x.Timestamp > endTime.Value);
            //list2.RemoveAll(x => x.Timestamp < startTime || endTime != null && x.Timestamp > endTime.Value);
            int count = list1.Count;

            var parabolicSarList1 = ParabolicSar.GetParabolicSar(quoteList1, .0005f, .005f).ToList();
            parabolicSarList1.RemoveAll(x => x.Timestamp < startTime || endTime != null && x.Timestamp > endTime.Value);
            var parabolicSarList2 = ParabolicSar.GetParabolicSar(quoteList1, .0005f, .005f).ToList();
            parabolicSarList2.RemoveAll(x => x.Timestamp < startTime || endTime != null && x.Timestamp > endTime.Value);

            //for (int i = 1; i < parabolicSarList1.Count - 1; i++)
            //{
            //    var pSar1 = parabolicSarList1[i];
            //    if (pSar1.Sar == pSar1.OriginalSar)
            //        logger.WriteLine($"{pSar1.Date:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F4}  /  {pSar1.IsReversal}");
            //    else
            //        logger.WriteLine($"{pSar1.Date:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F4}  /  {pSar1.IsReversal}  /  {pSar1.OriginalSar:F4}");
            //}
            //return 0;

            //for (int i = parabolicSarList1.Count - 100; i < parabolicSarList1.Count - 1; i++)
            //{
            //    var pSar2 = parabolicSarList2.Find(x => x.Date > list1[i].Timestamp);
            //    var b = list2.Find(x => x.Timestamp == pSar2.Date);
            //    if (pSar2.Sar == pSar2.OriginalSar)
            //        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {b.Timestamp:yyyy-MM-dd HH:mm:ss} \t {b.Open} / {b.High} / {b.Low} / {b.Close} \t {pSar2.Sar:F4}  /  {pSar2.IsReversal}");
            //    else
            //        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {b.Timestamp:yyyy-MM-dd HH:mm:ss} \t {b.Open} / {b.High} / {b.Low} / {b.Close} \t {pSar2.Sar:F4}  /  {pSar2.IsReversal}  /  {pSar2.OriginalSar:F4}");
            //}
            //return 0;

            int tryCount = 0;
            double minHeight = 0, maxHeight = 0;
            double totalProfit = 0;
            double finalPercent = 1;
            double finalPercent2 = 1;
            double finalPercent3 = 1;
            double finalPercent5 = 1;
            double finalPercent10 = 1;
            int position = 0;
            int positionEntryPrice = 0;

            int profitCount = 0, lossCount = 0, closeCount = 0, stopCount = 0, csrCount = 0;

            for (int i = 1; i < count - 1; i++)
            {
                var pSar1 = parabolicSarList1[i];
                //var pSar2 = parabolicSarList2.Find(x => x.Date > pSar1.Date);
                var pSar2 = parabolicSarList2[i];
                pSar2.Sar = null;
                if (position == 0)
                {
                    if (list1[i].Timestamp.Hour >= 0 && list1[i].Timestamp.Hour <= 3) continue;
                    if (list1[i].Timestamp.DayOfWeek == DayOfWeek.Saturday) continue;
                    if (pSar1.OriginalSar.Value < pSar1.Sar.Value && (pSar2.Sar == null || pSar2.Sar.Value >= pSar1.Sar.Value))
                    {
                        position = -1;
                        positionEntryPrice = (int)pSar1.OriginalSar.Value;
                        //if (list1[i].Timestamp.DayOfWeek == DayOfWeek.Saturday || list1[i].Timestamp.DayOfWeek == DayOfWeek.Sunday)
                        //    logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t <SHORT>", ConsoleColor.Red);
                        //else
                        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t <SHORT>");
                    }
                    else if (pSar1.OriginalSar.Value > pSar1.Sar.Value && (pSar2.Sar == null || pSar2.Sar.Value <= pSar1.Sar.Value))
                    {
                        position = 1;
                        positionEntryPrice = (int)pSar1.OriginalSar.Value;
                        //if (list1[i].Timestamp.DayOfWeek == DayOfWeek.Saturday || list1[i].Timestamp.DayOfWeek == DayOfWeek.Sunday)
                        //    logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t <LONG>", ConsoleColor.Red);
                        //else
                        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t <LONG>");
                    }
                }
                else if (position == 1)
                {
                    if ((double)list1[i].Low / positionEntryPrice < 1 - stopLoss || pSar1.IsReversal.Value && pSar1.OriginalSar.Value < pSar1.Sar.Value)
                    {
                        tryCount++;
                        int close = (int)Math.Ceiling(positionEntryPrice * (1 - stopLoss));
                        if (pSar1.IsReversal.Value) close = (int)Math.Max(close, pSar1.OriginalSar.Value);
                        int profit = close - positionEntryPrice;
                        if (profit > 0) profitCount++;
                        else lossCount++;
                        totalProfit += profit - positionEntryPrice * takerFee;
                        finalPercent *= (double)close / positionEntryPrice - takerFee;
                        finalPercent2 *= 1 + ((double)close / positionEntryPrice - takerFee - 1) * 2;
                        finalPercent3 *= 1 + ((double)close / positionEntryPrice - takerFee - 1) * 3;
                        finalPercent5 *= 1 + ((double)close / positionEntryPrice - takerFee - 1) * 5;
                        finalPercent10 *= 1 + ((double)close / positionEntryPrice - takerFee - 1) * 10;
                        if (minHeight > (double)profit / positionEntryPrice) minHeight = (double)profit / positionEntryPrice;
                        if (maxHeight < (double)profit / positionEntryPrice) maxHeight = (double)profit / positionEntryPrice;
                        if (profit > 0)
                            logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t entry = {positionEntryPrice} \t profit = {profit} \t total = {totalProfit} \t % = {finalPercent:F4} / {finalPercent2:F4}");
                        else
                            logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t entry = {positionEntryPrice} \t profit = {profit} \t total = {totalProfit} \t % = {finalPercent:F4} / {finalPercent2:F4}", ConsoleColor.Red);
                        //position = -1;
                        //positionEntryPrice = close;
                        position = 0;
                        if (pSar1.IsReversal.Value)
                        {
                            csrCount++;
                            i--;
                        }
                        else
                        {
                            stopCount++;
                        }
                    }
                    else if ((double)list1[i].High / positionEntryPrice > 1 + closeLimit)
                    {
                        tryCount++;
                        int close = (int)Math.Floor(positionEntryPrice * (1 + closeLimit));
                        int profit = close - positionEntryPrice;
                        profitCount++;
                        closeCount++;
                        totalProfit += profit - positionEntryPrice * makerFee;
                        finalPercent *= (double)close / positionEntryPrice - makerFee;
                        finalPercent2 *= 1 + ((double)close / positionEntryPrice - makerFee - 1) * 2;
                        finalPercent3 *= 1 + ((double)close / positionEntryPrice - makerFee - 1) * 3;
                        finalPercent5 *= 1 + ((double)close / positionEntryPrice - makerFee - 1) * 5;
                        finalPercent10 *= 1 + ((double)close / positionEntryPrice - makerFee - 1) * 10;
                        if (minHeight > (double)profit / positionEntryPrice) minHeight = (double)profit / positionEntryPrice;
                        if (maxHeight < (double)profit / positionEntryPrice) maxHeight = (double)profit / positionEntryPrice;
                        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t entry = {positionEntryPrice} \t profit = {profit} \t total = {totalProfit} \t % = {finalPercent:F4} / {finalPercent2:F4}");
                        //position = -1;
                        //positionEntryPrice = close;
                        position = 0;
                    }
                    else if (pSar1.IsReversal.Value && pSar1.OriginalSar.Value >= pSar1.Sar.Value)
                    {
                        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar} / {pSar1.IsReversal} / {pSar1.OriginalSar:F0} \t \t ERROR", ConsoleColor.Red);
                    }
                }
                else if (position == -1)
                {
                    if ((double)positionEntryPrice / list1[i].High < 1 - stopLoss || pSar1.IsReversal.Value && pSar1.OriginalSar.Value > pSar1.Sar.Value)
                    {
                        tryCount++;
                        int close = (int)Math.Floor(positionEntryPrice / (1 - stopLoss));
                        if (pSar1.IsReversal.Value) close = (int)Math.Min(close, pSar1.OriginalSar.Value);
                        int profit = positionEntryPrice - close;
                        if (profit > 0) profitCount++;
                        else lossCount++;
                        totalProfit += profit - positionEntryPrice * takerFee;
                        finalPercent *= (double)positionEntryPrice / close - takerFee;
                        finalPercent2 *= 1 + ((double)positionEntryPrice / close - takerFee - 1) * 2;
                        finalPercent3 *= 1 + ((double)positionEntryPrice / close - takerFee - 1) * 3;
                        finalPercent5 *= 1 + ((double)positionEntryPrice / close - takerFee - 1) * 5;
                        finalPercent10 *= 1 + ((double)positionEntryPrice / close - takerFee - 1) * 10;
                        if (minHeight > (double)profit / positionEntryPrice) minHeight = (double)profit / positionEntryPrice;
                        if (maxHeight < (double)profit / positionEntryPrice) maxHeight = (double)profit / positionEntryPrice;
                        if (profit > 0)
                            logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t entry = {positionEntryPrice} \t profit = {profit} \t total = {totalProfit} \t % = {finalPercent:F4} / {finalPercent2:F4}");
                        else
                            logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t entry = {positionEntryPrice} \t profit = {profit} \t total = {totalProfit} \t % = {finalPercent:F4} / {finalPercent2:F4}", ConsoleColor.Red);
                        //position = 1;
                        //positionEntryPrice = close;
                        position = 0;
                        if (pSar1.IsReversal.Value)
                        {
                            csrCount++;
                            i--;
                        }
                        else
                        {
                            stopCount++;
                        }
                    }
                    else if ((double)positionEntryPrice / list1[i].Low > 1 + closeLimit)
                    {
                        tryCount++;
                        int close = (int)Math.Ceiling(positionEntryPrice / (1 + closeLimit));
                        int profit = positionEntryPrice - close;
                        profitCount++;
                        closeCount++;
                        totalProfit += profit - positionEntryPrice * makerFee;
                        finalPercent *= (double)positionEntryPrice / close - makerFee;
                        finalPercent2 *= 1 + ((double)positionEntryPrice / close - makerFee - 1) * 2;
                        finalPercent3 *= 1 + ((double)positionEntryPrice / close - makerFee - 1) * 3;
                        finalPercent5 *= 1 + ((double)positionEntryPrice / close - makerFee - 1) * 5;
                        finalPercent10 *= 1 + ((double)positionEntryPrice / close - makerFee - 1) * 10;
                        if (minHeight > (double)profit / positionEntryPrice) minHeight = (double)profit / positionEntryPrice;
                        if (maxHeight < (double)profit / positionEntryPrice) maxHeight = (double)profit / positionEntryPrice;
                        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t entry = {positionEntryPrice} \t profit = {profit} \t total = {totalProfit} \t % = {finalPercent:F4} / {finalPercent2:F4}");
                        //position = 1;
                        //positionEntryPrice = close;
                        position = 0;
                    }
                    else if ((double)positionEntryPrice / list1[i].Low > 1 + closeLimit)
                    {
                        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar} / {pSar1.IsReversal} / {pSar1.OriginalSar:F0} \t \t ERROR", ConsoleColor.Red);
                    }
                }
            }
            double avgProfit = totalProfit / tryCount;
            string result = $"{startTime} ~ {endTime} ({totalDays} days) \t {start1:F6} / {step1:F6} / {max1:F6} \t close = {closeLimit:F4} \t stop = {stopLoss:F4}" +
                $"\r\ncount = {count} / {parabolicSarList1.Count} \t try = {tryCount} / {profitCount} / {lossCount} : {closeCount} / {stopCount} / {csrCount} \t h = {minHeight:F4} / {maxHeight:F4} \t p = {totalProfit:F2} \t avg = {avgProfit:F2} \t % = {finalPercent:F4} / {finalPercent2:F4} / {finalPercent3:F4} / {finalPercent5:F4} / {finalPercent10:F4}";
            logger.WriteLine($"\r\n{result}");
            return result;
        }

    }

}
