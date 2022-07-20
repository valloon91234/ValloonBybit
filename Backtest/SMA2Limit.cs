using IO.Swagger.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Valloon.Indicators;
using Valloon.Trading;
using Valloon.Utils;

namespace Valloon.Trading.Backtest
{
    class CandleQuoteExtended : CandleQuote
    {
        public float SMA_1 { get; set; }
        public float SMA_2 { get; set; }
        public float RSI { get; set; }
    }

    static class SMA2Limit
    {
        static readonly string SYMBOL = BybitLinearApiHelper.SYMBOL_ALGOUSDT;

        public static void Run()
        {
            Program.MoveWindow(20, 0, 1600, 140);

            //Loader.LoadCSV(SYMBOL, 1, new DateTime(2022, 1, 29, 0, 0, 0, DateTimeKind.Utc)); return;
            //Loader.Load(SYMBOL, "1m", new DateTime(2022, 5, 8, 0, 0, 0, DateTimeKind.Utc)); return;

            {
                Benchmark();
                return;
            }

            //ThreadPool.QueueUserWorkItem(state => runHCS(new DateTime(2021, 7, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        static float Benchmark()
        {
            const int buyOrSell = 2;
            const int binSize = 1;

            const float makerFee = 0.0003f;
            const float takerFee = 0.003f;
            const float loss = 0.05f;

            DateTime startTime = new DateTime(2022, 5, 4, 0, 0, 0, DateTimeKind.Utc);
            //DateTime startTime = new DateTime(2022, 3, 15, 0, 0, 0, DateTimeKind.Utc);
            DateTime? endTime = null;// new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var list1m = Dao.SelectAll(SYMBOL, "1");
            var list = Loader.LoadBinListFrom1m(binSize, list1m);
            var quoteList1 = new List<CandleQuote>();
            foreach (var t in list)
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
            list.RemoveAll(x => x.Timestamp < startTime || endTime != null && x.Timestamp > endTime.Value);
            int count = list.Count;
            int totalDays = (int)(list[count - 1].Timestamp - list[0].Timestamp).TotalDays;
            Logger logger = new Logger($"{DateTime.Now:yyyy-MM-dd  HH.mm.ss}    EMA2    buyOrSell = {buyOrSell}    bin = {binSize}    fee = {makerFee} - {takerFee}    {startTime:yyyy-MM-dd} ~ {endTime:yyyy-MM-dd} ({totalDays:N0}) days");
            logger.WriteLine("\n" + logger.LogFilename + "\n");
            logger.WriteLine($"{count} loaded. ({totalDays:N0} days)");
            Console.Title = logger.LogFilename;
            //{
            //    int rsiLength = 14;
            //    var rsiList = Rsi.GetRsi(quoteList1, rsiLength).ToList();
            //    rsiList.RemoveAll(x => x.Timestamp < startTime || endTime != null && x.Timestamp > endTime.Value);
            //    int smaLength = 72;
            //    var smaList = Sma.GetSma(quoteList1, smaLength).ToList();
            //    smaList.RemoveAll(x => x.Timestamp < startTime || endTime != null && x.Timestamp > endTime.Value);
            //    for (int i = 0; i < count; i++)
            //    {
            //        logger.WriteLine($"{list[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list[i].Open} / {list[i].High} / {list[i].Low} / {list[i].Close} \t sma<{smaLength}> = {smaList[i].Sma:F4} \t rsa<{rsiLength}> = {rsiList[i].Rsi:F4}");
            //    }
            //    return 0;
            //}
            List<Dictionary<string, float>> topList = new List<Dictionary<string, float>>();
            for (int smaLength1 = 6; smaLength1 <= 144; smaLength1 += 6)
            //int smaLength1 = 144;
            {
                var smaList1 = Sma.GetSma(quoteList1, smaLength1).ToList();
                smaList1.RemoveAll(x => x.Timestamp < startTime || endTime != null && x.Timestamp > endTime.Value);

                for (int smaLength2 = 3; smaLength2 <= smaLength1 / 2; smaLength2 += 3)
                //int smaLength2 = 72;
                {
                    var smaList2 = Sma.GetSma(quoteList1, smaLength2).ToList();
                    smaList2.RemoveAll(x => x.Timestamp < startTime || endTime != null && x.Timestamp > endTime.Value);

                    for (float limitX = 0.005f; limitX <= 0.05f; limitX += .0025f)
                    //float limitX = .02f;
                    {
                        for (float closeX = 0.005f; closeX <= 0.05f; closeX += .0025f)
                        //float closeX = .02f;
                        {
                            for (float stopX = 0.01f; stopX <= 0.05f; stopX += .0025f)
                            //float StopX = .012f;
                            {
                                int tryCount = 0;
                                int succeedCount = 0, failedCount = 0;
                                float finalPercent = 1;
                                int position = 0;
                                int positionEntryPrice = 0;
                                for (int i = 1; i < count - 1; i++)
                                {
                                    if (position == 0)
                                    {
                                        var sma1 = smaList1[i - 1].Sma.Value;
                                        var sma2 = smaList2[i - 1].Sma.Value;
                                        int limitPrice;
                                        if (buyOrSell == 1 && (limitPrice = (int)Math.Ceiling(sma2 * (1 - limitX))) > sma1 && limitPrice > list[i].Low)
                                        {
                                            position = 1;
                                            positionEntryPrice = limitPrice;
                                        }
                                        else if (buyOrSell == 2 && (limitPrice = (int)Math.Floor(sma2 * (1 + limitX))) < sma1 && limitPrice < list[i].High)
                                        {
                                            position = -1;
                                            positionEntryPrice = limitPrice;
                                        }
                                    }
                                    else if (position == 1)
                                    {
                                        int closePrice = (int)Math.Floor(positionEntryPrice * (1 + closeX));
                                        int stopPrice = (int)Math.Ceiling(positionEntryPrice * (1 - stopX));
                                        if (list[i].Low < stopPrice)
                                        {
                                            tryCount++;
                                            failedCount++;
                                            finalPercent *= 1 - loss;
                                            position = 0;
                                            positionEntryPrice = 0;
                                        }
                                        else if (list[i].High > closePrice)
                                        {
                                            tryCount++;
                                            succeedCount++;
                                            finalPercent *= 1 + loss * (closeX - makerFee) / (stopX + takerFee);
                                            position = 0;
                                            positionEntryPrice = 0;
                                        }
                                    }
                                    else if (position == -1)
                                    {
                                        int closePrice = (int)Math.Ceiling(positionEntryPrice * (1 - closeX));
                                        int stopPrice = (int)Math.Floor(positionEntryPrice * (1 + stopX));
                                        if (list[i].High > stopPrice)
                                        {
                                            tryCount++;
                                            failedCount++;
                                            finalPercent *= 1 - loss;
                                            position = 0;
                                            positionEntryPrice = 0;
                                        }
                                        else if (list[i].Low < closePrice)
                                        {
                                            tryCount++;
                                            succeedCount++;
                                            finalPercent *= 1 + loss * (closeX - makerFee) / (stopX + takerFee);
                                            position = 0;
                                            positionEntryPrice = 0;
                                        }
                                    }
                                }
                                float successRate = failedCount > 0 ? (float)succeedCount / failedCount : succeedCount;
                                if (finalPercent > 1f && succeedCount > 0)
                                {
                                    Dictionary<string, float> dic = new Dictionary<string, float>
                                    {
                                        { "buyOrSell", buyOrSell },
                                        { "binSize", binSize },
                                        { "makerFee", makerFee },
                                        { "takerFee", takerFee },
                                        { "smaLength1", smaLength1 },
                                        { "smaLength2", smaLength2 },
                                        { "limitX", limitX },
                                        { "closeX", closeX },
                                        { "stopX", stopX },
                                        { "tryCount", tryCount },
                                        { "succeedCount", succeedCount },
                                        { "failedCount", failedCount },
                                        { "finalPercent", finalPercent },
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
                                            if (topList[i]["finalPercent"] > finalPercent)
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
                                    logger.WriteLine($"sma = {smaLength1} / {smaLength2} \t limit = {limitX:F4} / {closeX:F4} / {stopX:F4} \t count = {tryCount} / {succeedCount} / {failedCount} / {successRate:F2} \t % = {finalPercent:F4}");
                                }
                                //else if (finalPercent > .5f && succeedCount > 0)
                                //{
                                //    logger.WriteLine($"sma = {smaLength1} / {smaLength2} \t limit = {limitX:F4} / {closeX:F4} / {stopX:F4} \t count = {tryCount} / {succeedCount} / {failedCount} / {successRate:F2} \t % = {finalPercent:F4}", ConsoleColor.DarkGray, false);
                                //}
                            }
                        }
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
            //const float makerFee = 0.0003f;
            //const float takerFee = 0.002f;
            const float makerFee = 0.001f;
            const float takerFee = 0.002f;
            const int binSize1 = 5;
            //const int binSize2 = 60;
            float start1 = 0.0015f;
            float step1 = 0.0015f;
            float max1 = 0.03f;
            //float start2 = 0.00044f;
            //float step2 = start2;
            //float max2 = 0.017f;
            float closeLimit = 0.054f;
            float stopLoss = 0.012f;

            //DateTime startTime = new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc);
            //DateTime? endTime = null;
            var list1m = Dao.SelectAll(SYMBOL, "1m");
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

            var parabolicSarList1 = ParabolicSar.GetParabolicSar(quoteList1, step1, max1, start1).ToList();
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
            float minHeight = 0, maxHeight = 0;
            float totalProfit = 0;
            float finalPercent = 1;
            float finalPercent2 = 1;
            float finalPercent3 = 1;
            float finalPercent5 = 1;
            float finalPercent10 = 1;
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
                    if ((float)list1[i].Low / positionEntryPrice < 1 - stopLoss || pSar1.IsReversal.Value && pSar1.OriginalSar.Value < pSar1.Sar.Value)
                    {
                        tryCount++;
                        int close = (int)Math.Ceiling(positionEntryPrice * (1 - stopLoss));
                        if (pSar1.IsReversal.Value) close = (int)Math.Max(close, pSar1.OriginalSar.Value);
                        int profit = close - positionEntryPrice;
                        if (profit > 0) profitCount++;
                        else lossCount++;
                        totalProfit += profit - positionEntryPrice * takerFee;
                        finalPercent *= (float)close / positionEntryPrice - takerFee;
                        finalPercent2 *= 1 + ((float)close / positionEntryPrice - takerFee - 1) * 2;
                        finalPercent3 *= 1 + ((float)close / positionEntryPrice - takerFee - 1) * 3;
                        finalPercent5 *= 1 + ((float)close / positionEntryPrice - takerFee - 1) * 5;
                        finalPercent10 *= 1 + ((float)close / positionEntryPrice - takerFee - 1) * 10;
                        if (minHeight > (float)profit / positionEntryPrice) minHeight = (float)profit / positionEntryPrice;
                        if (maxHeight < (float)profit / positionEntryPrice) maxHeight = (float)profit / positionEntryPrice;
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
                    else if ((float)list1[i].High / positionEntryPrice > 1 + closeLimit)
                    {
                        tryCount++;
                        int close = (int)Math.Floor(positionEntryPrice * (1 + closeLimit));
                        int profit = close - positionEntryPrice;
                        profitCount++;
                        closeCount++;
                        totalProfit += profit - positionEntryPrice * makerFee;
                        finalPercent *= (float)close / positionEntryPrice - makerFee;
                        finalPercent2 *= 1 + ((float)close / positionEntryPrice - makerFee - 1) * 2;
                        finalPercent3 *= 1 + ((float)close / positionEntryPrice - makerFee - 1) * 3;
                        finalPercent5 *= 1 + ((float)close / positionEntryPrice - makerFee - 1) * 5;
                        finalPercent10 *= 1 + ((float)close / positionEntryPrice - makerFee - 1) * 10;
                        if (minHeight > (float)profit / positionEntryPrice) minHeight = (float)profit / positionEntryPrice;
                        if (maxHeight < (float)profit / positionEntryPrice) maxHeight = (float)profit / positionEntryPrice;
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
                    if ((float)positionEntryPrice / list1[i].High < 1 - stopLoss || pSar1.IsReversal.Value && pSar1.OriginalSar.Value > pSar1.Sar.Value)
                    {
                        tryCount++;
                        int close = (int)Math.Floor(positionEntryPrice / (1 - stopLoss));
                        if (pSar1.IsReversal.Value) close = (int)Math.Min(close, pSar1.OriginalSar.Value);
                        int profit = positionEntryPrice - close;
                        if (profit > 0) profitCount++;
                        else lossCount++;
                        totalProfit += profit - positionEntryPrice * takerFee;
                        finalPercent *= (float)positionEntryPrice / close - takerFee;
                        finalPercent2 *= 1 + ((float)positionEntryPrice / close - takerFee - 1) * 2;
                        finalPercent3 *= 1 + ((float)positionEntryPrice / close - takerFee - 1) * 3;
                        finalPercent5 *= 1 + ((float)positionEntryPrice / close - takerFee - 1) * 5;
                        finalPercent10 *= 1 + ((float)positionEntryPrice / close - takerFee - 1) * 10;
                        if (minHeight > (float)profit / positionEntryPrice) minHeight = (float)profit / positionEntryPrice;
                        if (maxHeight < (float)profit / positionEntryPrice) maxHeight = (float)profit / positionEntryPrice;
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
                    else if ((float)positionEntryPrice / list1[i].Low > 1 + closeLimit)
                    {
                        tryCount++;
                        int close = (int)Math.Ceiling(positionEntryPrice / (1 + closeLimit));
                        int profit = positionEntryPrice - close;
                        profitCount++;
                        closeCount++;
                        totalProfit += profit - positionEntryPrice * makerFee;
                        finalPercent *= (float)positionEntryPrice / close - makerFee;
                        finalPercent2 *= 1 + ((float)positionEntryPrice / close - makerFee - 1) * 2;
                        finalPercent3 *= 1 + ((float)positionEntryPrice / close - makerFee - 1) * 3;
                        finalPercent5 *= 1 + ((float)positionEntryPrice / close - makerFee - 1) * 5;
                        finalPercent10 *= 1 + ((float)positionEntryPrice / close - makerFee - 1) * 10;
                        if (minHeight > (float)profit / positionEntryPrice) minHeight = (float)profit / positionEntryPrice;
                        if (maxHeight < (float)profit / positionEntryPrice) maxHeight = (float)profit / positionEntryPrice;
                        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar:F0} / {pSar1.IsReversal} \t entry = {positionEntryPrice} \t profit = {profit} \t total = {totalProfit} \t % = {finalPercent:F4} / {finalPercent2:F4}");
                        //position = 1;
                        //positionEntryPrice = close;
                        position = 0;
                    }
                    else if ((float)positionEntryPrice / list1[i].Low > 1 + closeLimit)
                    {
                        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar} / {pSar1.IsReversal} / {pSar1.OriginalSar:F0} \t \t ERROR", ConsoleColor.Red);
                    }
                }
            }
            float avgProfit = totalProfit / tryCount;
            string result = $"{startTime} ~ {endTime} ({totalDays} days) \t {start1:F6} / {step1:F6} / {max1:F6} \t close = {closeLimit:F4} \t stop = {stopLoss:F4}" +
                $"\r\ncount = {count} / {parabolicSarList1.Count} \t try = {tryCount} / {profitCount} / {lossCount} : {closeCount} / {stopCount} / {csrCount} \t h = {minHeight:F4} / {maxHeight:F4} \t p = {totalProfit:F2} \t avg = {avgProfit:F2} \t % = {finalPercent:F4} / {finalPercent2:F4} / {finalPercent3:F4} / {finalPercent5:F4} / {finalPercent10:F4}";
            logger.WriteLine($"\r\n{result}");
            return result;
        }

    }

}
