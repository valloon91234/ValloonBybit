using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Valloon.Indicators;
using Valloon.Trading;

namespace Valloon.Trading.Backtest
{
    static class PSar
    {
        static string SYMBOL = BybitLinearApiHelper.SYMBOL_ALGOUSDT;

        public static void Run()
        {
            Program.MoveWindow(20, 0, 1600, 140);

            Loader.WriteCSV("ETHUSDT", 1, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc)); return;
            //Loader.LoadCSV(SYMBOL, 1, new DateTime(2022, 1, 29, 0, 0, 0, DateTimeKind.Utc)); return;
            //Loader.Load(SYMBOL, 1, new DateTime(2022, 5, 10, 0, 0, 0, DateTimeKind.Utc)); return;

            {
                Benchmark();
                return;
            }

            //ThreadPool.QueueUserWorkItem(state => runHCS(new DateTime(2021, 7, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        static float Benchmark()
        {
            const float makerFee = 0.0015f;
            const float takerFee = 0.002f;
            const int interval = 15;

            //DateTime startTime = new DateTime(2022, 3, 16, 0, 0, 0, DateTimeKind.Utc);
            DateTime startTime = new DateTime(2022, 5, 5, 0, 0, 0, DateTimeKind.Utc);
            DateTime? endTime = null;
            var list1m = new List<CandleQuote>();// Dao.SelectAll(SYMBOL, "1");
            var list1 = Loader.LoadBinListFrom1m(interval, list1m);
            //List<SolBin> list2 = LoadBinListFrom1m(binSize2, list1m);
            int count1 = list1.Count;
            int totalDays = (int)((endTime == null ? DateTime.UtcNow : endTime.Value) - startTime).TotalDays;
            Logger logger = new Logger($"{DateTime.Now:yyyy-MM-dd  HH.mm.ss}    BenchmarkSAR    bin = {interval}    fee = {makerFee} - {takerFee}    {startTime:yyyy-MM-dd} ~ {endTime:yyyy-MM-dd} ({totalDays:N0}) days");
            logger.WriteLine("\n" + logger.LogFilename + "\n");
            logger.WriteLine($"{count1} loaded. ({totalDays:N0} days)");
            Console.Title = logger.LogFilename;
            List<Dictionary<string, float>> topList = new List<Dictionary<string, float>>();

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

            //List<ParabolicSarResult> parabolicSarList2 = ParabolicSar.GetParabolicSar(quoteList1, .0005f, .005f).ToList();
            //parabolicSarList2.RemoveAll(x => x.Date < startTime || endTime != null && x.Date > endTime.Value);

            for (float stopLoss = 0.01f; stopLoss <= 0.031f; stopLoss += .005f)
            //float stopLoss = .012f;
            {
                //for (float closeLimit = stopLoss; closeLimit <= 0.05f; closeLimit += .001f)
                float closeLimit = 0;
                {
                    for (float step1 = 0.001f; step1 <= 0.04f; step1 += .001f)
                    //float step1 = .0015f;
                    {
                        //for (float start1 = 0.001f; start1 <= 0.05f; start1 += .001f)
                        float start1 = step1;
                        //float start1 = .00186f;
                        {
                            for (float max1 = .01f; max1 <= 0.4; max1 += .01f)
                            //float max1 = .03f;
                            {
                                if (step1 >= max1) continue;

                                List<ParabolicSarResult> parabolicSarList1 = ParabolicSar.GetParabolicSar(quoteList1, step1, max1, start1).ToList();
                                parabolicSarList1.RemoveAll(x => x.Timestamp < startTime || endTime != null && x.Timestamp > endTime.Value);
                                int count = parabolicSarList1.Count;

                                //for (float step2 = 0.0004f; step2 <= 0.0005f; step2 += .00001f)
                                //float step2 = 0.00044f;
                                {
                                    //for (float start2 = 0.0004f; start2 <= 0.0005f; start2 += .00001f)
                                    //float start2 = step2;
                                    {
                                        //for (float max2 = .015f; max2 <= 0.02; max2 += .001f)
                                        //float max2 = 0.017f;
                                        {
                                            //if (step2 >= max2) continue;
                                            //Console.WriteLine($"\r\n start = {start} \t step = {step} \t max = {max} \t count = {count} / {smaList.Count} / {parabolicSarList.Count}\r\n");

                                            //List<ParabolicSarResult> parabolicSarList2 = ParabolicSar.GetParabolicSar(quoteList2, step2, max2, start2).ToList();
                                            //parabolicSarList2.RemoveAll(x => x.Date < startTime || endTime != null && x.Date > endTime.Value);

                                            int tryCount = 0;
                                            int succeedCount = 0, failedCount = 0;
                                            float minHeight = 0, maxHeight = 0;
                                            float totalProfit = 0;
                                            float finalPercent = 1;
                                            int position = 0;
                                            int positionEntryPrice = 0;
                                            for (int i = 1; i < count - 1; i++)
                                            {
                                                var pSar1 = parabolicSarList1[i];
                                                //var pSar2 = parabolicSarList2.Find(x => x.Date > pSar1.Date);
                                                //var pSar2 = parabolicSarList2[i];

                                                if (position == 0)
                                                {
                                                    if (pSar1.IsReversal.Value)
                                                    {
                                                        //if (pSar1.OriginalSar.Value < pSar1.Sar.Value && pSar2.Sar.Value >= pSar1.Sar.Value)
                                                        if (pSar1.OriginalSar.Value < pSar1.Sar.Value)
                                                        {
                                                            position = -1;
                                                            positionEntryPrice = (int)pSar1.OriginalSar.Value;
                                                        }
                                                        //else if (pSar1.OriginalSar.Value > pSar1.Sar.Value && pSar2.Sar.Value <= pSar1.Sar.Value)
                                                        else if (pSar1.OriginalSar.Value > pSar1.Sar.Value)
                                                        {
                                                            position = 1;
                                                            positionEntryPrice = (int)pSar1.OriginalSar.Value;
                                                        }
                                                    }
                                                    //else if (pSar2.IsReversal.Value && pSar1.Sar > list[i].Open && pSar2.Sar >= pSar1.Sar)
                                                    //{
                                                    //    position = -1;
                                                    //    positionEntryPrice = (int)parabolicSarList2[i - 1].Sar.Value;
                                                    //}
                                                }
                                                else if (position == 1)
                                                {
                                                    if ((float)list1[i].Low / positionEntryPrice < 1 - stopLoss || pSar1.IsReversal.Value && pSar1.OriginalSar.Value < pSar1.Sar.Value)
                                                    {
                                                        tryCount++;
                                                        int close = (int)Math.Ceiling(positionEntryPrice * (1 - stopLoss));
                                                        if (pSar1.IsReversal.Value) close = (int)Math.Max(close, pSar1.OriginalSar.Value);
                                                        int profit = close - positionEntryPrice;
                                                        totalProfit += profit - positionEntryPrice * takerFee;
                                                        finalPercent *= (float)close / positionEntryPrice - takerFee;
                                                        if (minHeight > (float)profit / positionEntryPrice) minHeight = (float)profit / positionEntryPrice;
                                                        if (maxHeight < (float)profit / positionEntryPrice) maxHeight = (float)profit / positionEntryPrice;
                                                        if (profit > 0) succeedCount++;
                                                        else failedCount++;
                                                        position = 0;
                                                        if (pSar1.IsReversal.Value) i--;
                                                    }
                                                    //else if ((float)list1[i].High / positionEntryPrice > 1 + closeLimit)
                                                    //{
                                                    //    tryCount++;
                                                    //    int close = (int)Math.Floor(positionEntryPrice * (1 + closeLimit));
                                                    //    int profit = close - positionEntryPrice;
                                                    //    totalProfit += profit - positionEntryPrice * makerFee;
                                                    //    finalPercent *= (float)close / positionEntryPrice - makerFee;
                                                    //    if (minHeight > (float)profit / positionEntryPrice) minHeight = (float)profit / positionEntryPrice;
                                                    //    if (maxHeight < (float)profit / positionEntryPrice) maxHeight = (float)profit / positionEntryPrice;
                                                    //    if (profit > 0) succeedCount++;
                                                    //    else failedCount++;
                                                    //    //position = -1;
                                                    //    //positionEntryPrice = close;
                                                    //    position = 0;
                                                    //}
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
                                                        totalProfit += profit - positionEntryPrice * takerFee;
                                                        finalPercent *= (float)positionEntryPrice / close - takerFee;
                                                        if (minHeight > (float)profit / positionEntryPrice) minHeight = (float)profit / positionEntryPrice;
                                                        if (maxHeight < (float)profit / positionEntryPrice) maxHeight = (float)profit / positionEntryPrice;
                                                        if (profit > 0) succeedCount++;
                                                        else failedCount++;
                                                        //position = 1;
                                                        //positionEntryPrice = close;
                                                        position = 0;
                                                        if (pSar1.IsReversal.Value) i--;
                                                    }
                                                    //else if ((float)positionEntryPrice / list1[i].Low > 1 + closeLimit)
                                                    //{
                                                    //    tryCount++;
                                                    //    int close = (int)Math.Ceiling(positionEntryPrice / (1 + closeLimit));
                                                    //    int profit = positionEntryPrice - close;
                                                    //    totalProfit += profit - positionEntryPrice * makerFee;
                                                    //    finalPercent *= (float)positionEntryPrice / close - makerFee;
                                                    //    if (minHeight > (float)profit / positionEntryPrice) minHeight = (float)profit / positionEntryPrice;
                                                    //    if (maxHeight < (float)profit / positionEntryPrice) maxHeight = (float)profit / positionEntryPrice;
                                                    //    if (profit > 0) succeedCount++;
                                                    //    else failedCount++;
                                                    //    //position = 1;
                                                    //    //positionEntryPrice = close;
                                                    //    position = 0;
                                                    //}
                                                    else if (pSar1.IsReversal.Value && pSar1.OriginalSar.Value <= pSar1.Sar.Value)
                                                    {
                                                        logger.WriteLine($"{list1[i].Timestamp:yyyy-MM-dd HH:mm:ss} \t {list1[i].Open} / {list1[i].High} / {list1[i].Low} / {list1[i].Close} \t {pSar1.Sar} / {pSar1.IsReversal} / {pSar1.OriginalSar:F0} \t \t ERROR", ConsoleColor.Red);
                                                    }
                                                }
                                            }
                                            //if (position == 1)
                                            //{
                                            //    tryCount++;
                                            //    int close = (int)((list1.Last().Close + parabolicSarList1.Last().Sar.Value) / 2);
                                            //    int profit = close - positionEntryPrice;
                                            //    totalProfit += profit - positionEntryPrice * takerFee;
                                            //    finalPercent *= (float)close / positionEntryPrice - takerFee;
                                            //    if (minHeight > (float)profit / positionEntryPrice) minHeight = (float)profit / positionEntryPrice;
                                            //    if (maxHeight < (float)profit / positionEntryPrice) maxHeight = (float)profit / positionEntryPrice;
                                            //    position = 0;
                                            //}
                                            //else if (position == 2)
                                            //{
                                            //    tryCount++;
                                            //    int close = (int)((list1.Last().Close + parabolicSarList1.Last().Sar.Value) / 2);
                                            //    int profit = positionEntryPrice - close;
                                            //    totalProfit += profit - positionEntryPrice * takerFee;
                                            //    finalPercent *= (float)positionEntryPrice / close - takerFee;
                                            //    if (minHeight > (float)profit / positionEntryPrice) minHeight = (float)profit / positionEntryPrice;
                                            //    if (maxHeight < (float)profit / positionEntryPrice) maxHeight = (float)profit / positionEntryPrice;
                                            //    position = 0;
                                            //}
                                            totalProfit /= BybitLinearApiHelper.GetX(SYMBOL);
                                            float avgProfit = totalProfit / tryCount;
                                            float successRate = failedCount == 0 ? failedCount : ((float)succeedCount / failedCount);
                                            float score = (finalPercent - 1) / stopLoss;
                                            if (totalProfit > 0 && finalPercent > 1f && tryCount >= totalDays / 2)
                                            {
                                                Dictionary<string, float> dic = new Dictionary<string, float>
                                                {
                                                    { "bin1", interval },
                                                    { "start1", start1 },
                                                    { "step1", step1 },
                                                    { "max1", max1 },
                                                    //{ "bin2", binSize2 },
                                                    //{ "start2", start2 },
                                                    //{ "step2", step2 },
                                                    //{ "max2", max2 },
                                                    //{ "closeLimit",closeLimit },
                                                    { "stopLoss",stopLoss },
                                                    { "tryCount", tryCount },
                                                    { "succeedCount", succeedCount },
                                                    { "failedCount", failedCount },
                                                    { "successRate", successRate },
                                                    { "minHeight", minHeight },
                                                    { "maxHeight", maxHeight },
                                                    { "totalProfit", totalProfit },
                                                    { "avgProfit", avgProfit },
                                                    { "finalPercent", finalPercent },
                                                    { "score", score },
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
                                                        if (topList[i]["finalPercent"] > finalPercent ||
                                                                topList[i]["finalPercent"] == finalPercent && topList[i]["avgProfit"] > avgProfit)
                                                        //if (topList[i]["score"] > score)
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
                                                logger.WriteLine($"{start1:F6} / {step1:F6} / {max1:F6} \t t = {closeLimit:F4} / {stopLoss:F4} \t c = {tryCount} / {succeedCount} / {failedCount} / {successRate:F4} \t h = {minHeight:F4} / {maxHeight:F4} \t p = {totalProfit:F2} \t avg = {avgProfit:F2} \t % = {finalPercent:F4} / +{score:F4}");
                                            }
                                            else
                                            {
                                                logger.WriteLine($"{start1:F6} / {step1:F6} / {max1:F6} \t t = {closeLimit:F4} / {stopLoss:F4} \t c = {tryCount} / {succeedCount} / {failedCount} / {successRate:F4} \t h = {minHeight:F4} / {maxHeight:F4} \t p = {totalProfit:F2} \t avg = {avgProfit:F2} \t % = {finalPercent:F4}", ConsoleColor.DarkGray, false);
                                            }
                                        }
                                    }
                                }
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
            var list1m = new List<CandleQuote>();// Dao.SelectAll(SYMBOL, "1m");
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
