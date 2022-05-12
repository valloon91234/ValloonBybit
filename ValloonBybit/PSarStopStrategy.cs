//#define LICENSE_MODE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using IO.Swagger.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Skender.Stock.Indicators;
using Valloon.Stock.Indicators;
using Valloon.Utils;

/**
 * @author Valloon Present
 * @version 2022-02-10
 */
namespace Valloon.Trading
{
    public class PSarStopStrategy
    {
        private class ParamConfig
        {
            [JsonProperty("bin")]
            public int BinSize { get; set; }
            [JsonProperty("start")]
            public decimal PSarStart { get; set; }
            [JsonProperty("step")]
            public decimal PSarStep { get; set; }
            [JsonProperty("max")]
            public decimal PSarMax { get; set; }
            [JsonProperty("stop")]
            public decimal StopLoss { get; set; }
        }

        public void Run(Config config = null)
        {
            int loopIndex = 0;
            DateTime? lastLoopTime = null;
            decimal lastWalletBalance = 0;
            decimal targetEntryPrice = 0;
            ParamConfig param = null;
            Logger logger = null;
            DateTime fileModifiedTime = File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location);
            while (true)
            {
                try
                {
                    DateTime currentLoopTime = DateTime.UtcNow;
                    config = Config.Load(out bool configUpdated, lastLoopTime != null && lastLoopTime.Value.Day != currentLoopTime.Day);
                    var apiHelper = new BybitLinearApiHelper(config.ApiKey, config.ApiSecret, config.TestnetMode);
                    logger = new Logger($"{BybitLinearApiHelper.ServerTime:yyyy-MM-dd}");
                    if (configUpdated)
                    {
                        loopIndex = 0;
                        logger.WriteLine($"\r\n[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]  Config loaded.", ConsoleColor.Green);
                        logger.WriteLine(JObject.FromObject(config).ToString(Formatting.Indented));
                    }
                    string symbol = config.Symbol.ToUpper();
                    int symbolX = BybitLinearApiHelper.GetX(symbol);
                    if (param == null || BybitLinearApiHelper.ServerTime.Minute % 30 == 0 && BybitLinearApiHelper.ServerTime.Second < 5)
                    {
                        string url = $"https://raw.githubusercontent.com/valloon91234/_shared/master/bybit-algousdt-psar-0510.json";
                        string paramText = HttpClient2.HttpGet(url);
                        param = JsonConvert.DeserializeObject<ParamConfig>(paramText);
                        logger.WriteLine($"\r\n[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]  ParamMap loaded.", ConsoleColor.Green);
                        logger.WriteLine(JObject.FromObject(param).ToString(Formatting.Indented));
                        logger.WriteLine();
                    }
                    else if (configUpdated)
                    {
                        logger.WriteLine();
                    }

                    var candleList = apiHelper.GetCandleList(symbol, param.BinSize.ToString(), BybitLinearApiHelper.ServerTime.AddMinutes(-param.BinSize * 195));
                    var ticker = apiHelper.GetTicker(symbol);
                    var activeStopOrderList = apiHelper.GetActiveStopOrders(symbol);
                    decimal lastPrice = ticker.LastPrice.Value;
                    decimal markPrice = ticker.MarkPrice.Value;
                    var botStopOrderList = new List<LinearListStopOrderResult>();
                    foreach (var order in activeStopOrderList)
                        if (order.OrderLinkId.Contains("<BOT>")) botStopOrderList.Add(order);
                    var position = apiHelper.GetPosition(symbol, out var buyPosition, out var sellPosition);
                    var usdtBalance = apiHelper.GetWalletBalance("USDT");
                    var walletBalance = usdtBalance._WalletBalance.Value;
                    {
                        //decimal unavailableMarginPercent = 100m * position.OrderMargin.Value / position.WalletBalance.Value;
                        decimal unavailableMarginPercent = 100m * (walletBalance - position.OrderMargin.Value) / walletBalance;
                        string balanceChange = null;
                        if (lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        {
                            decimal value = (walletBalance - lastWalletBalance) / lastWalletBalance * 100;
                            if (value >= 0)
                                balanceChange = $"    ( +{value:N4} % )";
                            else
                                balanceChange = $"    ( {value:N4} % )";
                        }
                        logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss fff}  ({++loopIndex})]    $ {lastPrice:F2}  /  $ {markPrice:F3}    {walletBalance:N8} XBT    {botStopOrderList.Count} / {activeStopOrderList.Count} / {unavailableMarginPercent:N2} %{balanceChange}", ConsoleColor.White);
                    }
                    int qty = (int)(walletBalance * config.Leverage * (1 - (0.0006m * 2)));
                    if (qty > 50) qty = BybitLinearApiHelper.FixQty(qty);
                    decimal positionEntryPrice = 0;
                    int positionQty = (int)position.Size.Value;
                    {
                        string consoleTitle = $"$ {lastPrice:N2}  /  {walletBalance:N8}  /  {qty:N0} Cont";
                        if (positionQty != 0) consoleTitle += "  /  1 Position";
                        consoleTitle += $"    <{config.Username}>  |   {Config.APP_NAME}  v{fileModifiedTime:yyyy.MM.dd HH:mm:ss}   by  ValloonTrader.com";
                        Console.Title = consoleTitle;
                        if (positionQty != 0)
                        {
                            positionEntryPrice = position.EntryPrice.Value;
                            decimal leverage = 1 / (Math.Abs(positionEntryPrice - position.LiqPrice.Value) / positionEntryPrice);
                            decimal unrealisedPercent = 100m * position.UnrealisedPnl.Value / walletBalance;
                            decimal nowLoss = 100m * (positionEntryPrice - lastPrice) / (position.LiqPrice.Value - positionEntryPrice);
                            logger.WriteLine($"    <Position>    entry = {positionEntryPrice:F2} / {targetEntryPrice}    qty = {positionQty}    liq = {position.LiqPrice}    leverage = {leverage:F2}    {unrealisedPercent:N2} % / {nowLoss:N2} %", ConsoleColor.Green);
                        }
                    }

                    var quoteList = IndicatorHelper.ToQuote(candleList);
                    var parabolicSarList = quoteList.GetParabolicSar(param.PSarStep, param.PSarMax, param.PSarStart).ToList();
                    var lastPSar = parabolicSarList.Last();
                    string trend = null;
                    if (lastPSar.Sar > quoteList.Last().High)
                        trend = "\\/ Bearish \t ";
                    else if (lastPSar.Sar < quoteList.Last().Low)
                        trend = "/\\ Bullish \t ";
                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {trend}sar = {parabolicSarList[parabolicSarList.Count - 2].Sar:F4} / {lastPSar.Sar:F4} \t {parabolicSarList[parabolicSarList.Count - 2].IsReversal} / {lastPSar.IsReversal}", ConsoleColor.DarkGray);
                    if (config.Exit == 2)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  No order. (exit = {config.Exit})", ConsoleColor.DarkGray);
                        goto endLoop;
                    }
                    if (config.Leverage == 0)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  No order. (qty = {config.Leverage})", ConsoleColor.DarkGray);
                        goto endLoop;
                    }
                    if (positionQty == 0)
                    {
#if LICENSE_MODE
                        if (serverTime.Year != 2022 || serverTime.Month != 2)
                        {
                            List<string> cancelOrderList = new List<string>();
                            foreach (Order order in botOrderList)
                            {
                                cancelOrderList.Add(order.OrderID);
                            }
                            if (cancelOrderList.Count > 0)
                            {
                                var canceledOrders = apiHelper.CancelOrders(cancelOrderList);
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrders.Count} old orders have been canceled.");
                            }
                            logger.WriteLine($"This bot is too old. Please contact support.  https://valloontrader.com", ConsoleColor.Green);
                            goto endLoop;
                        }
#endif
                        //if (config.Exit == 1)
                        //{
                        //    List<string> cancelOrderList = new List<string>();
                        //    int canceledOrderCount = 0;
                        //    foreach (var order in botOrderList)
                        //    {
                        //        apiHelper.CancelActiveStopOrder(symbol, order.OrderId);
                        //        canceledOrderCount++;
                        //    }
                        //    if (canceledOrderCount > 0)
                        //        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} old orders have been canceled.");
                        //    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  No order. (exit = {config.Exit})", ConsoleColor.DarkGray);
                        //    goto endLoop;
                        //}
                        //{
                        //    List<string> cancelOrderList = new List<string>();
                        //    int canceledOrderCount = 0;
                        //    foreach (var order in botOrderList)
                        //    {
                        //        if (order.OrderLinkId.Contains("<STOP-CLOSE>") || order.OrderLinkId.Contains("<LIMIT-CLOSE>"))
                        //        {
                        //            apiHelper.CancelActiveStopOrder(symbol, order.OrderId);
                        //            canceledOrderCount++;
                        //        }
                        //    }
                        //    if (canceledOrderCount > 0)
                        //        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} old CLOSE orders have been canceled.");
                        //}
                    }

                    bool needOpenOrder = true;
                    if (positionQty > 0)
                    {
                        if (lastPSar.Sar.Value < quoteList.Last().Low)
                        {
                            decimal stopPrice = Math.Floor(lastPSar.Sar.Value * symbolX) / symbolX;
                            if (param.StopLoss > 0 && targetEntryPrice > 0)
                            {
                                var stopLossPrice = Math.Floor(targetEntryPrice * (1 - param.StopLoss) * symbolX) / symbolX;
                                stopPrice = Math.Max(stopPrice, stopLossPrice);
                            }
                            if (position.StopLoss == null || position.StopLoss.Value != stopPrice)
                                apiHelper.SetPositionStop(symbol, "Sell", null, stopPrice);
                        }
                        else
                        {
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Wrong position: qty = {positionQty}, stopLoss = {position.StopLoss}");
                            needOpenOrder = false;
                            if (param.StopLoss > 0 && (position.StopLoss == null || position.StopLoss.Value == 0))
                            {
                                decimal stopPrice = Math.Floor(positionEntryPrice * (1 - param.StopLoss) * symbolX) / symbolX;
                                apiHelper.SetPositionStop(symbol, "Sell", null, stopPrice);
                            }
                        }
                    }
                    else if (positionQty < 0)
                    {
                        if (lastPSar.Sar.Value > quoteList.Last().High)
                        {
                            decimal stopPrice = Math.Ceiling(lastPSar.Sar.Value * symbolX) / symbolX;
                            if (param.StopLoss > 0 && targetEntryPrice > 0)
                            {
                                var stopLossPrice = Math.Ceiling(targetEntryPrice * (1 + param.StopLoss) * symbolX) / symbolX;
                                stopPrice = Math.Min(stopPrice, stopLossPrice);
                            }
                            if (position.StopLoss == null || position.StopLoss.Value != stopPrice)
                                apiHelper.SetPositionStop(symbol, "Buy", null, stopPrice);
                        }
                        else
                        {
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Wrong position: qty = {positionQty}");
                            needOpenOrder = false;
                            if (param.StopLoss > 0 && (position.StopLoss == null || position.StopLoss.Value == 0))
                            {
                                decimal stopPrice = Math.Ceiling(positionEntryPrice * (1 + param.StopLoss) * symbolX) / symbolX;
                                apiHelper.SetPositionStop(symbol, "Buy", null, stopPrice);
                            }
                        }
                    }
                    //else
                    //{
                    //    logger.WriteLine($"        Invalid position qty: {positionQty}");
                    //}

                    if (needOpenOrder /* && BybitLinearApiHelper.ServerTime.Hour > 3 */ )
                    {
                        ConditionalRes newStopOpenOrder = null;
                        if (lastPSar.Sar.Value > quoteList.Last().High && (config.BuyOrSell == 3 || config.BuyOrSell == 1))
                        {
                            targetEntryPrice = Math.Ceiling(lastPSar.Sar.Value * symbolX) / symbolX;
                            decimal? stopLoss = null;
                            if (param.StopLoss > 0)
                                stopLoss = Math.Floor(positionEntryPrice * (1 - param.StopLoss) * symbolX) / symbolX;
                            newStopOpenOrder = new ConditionalRes
                            {
                                Side = "Buy",
                                Symbol = symbol,
                                OrderType = "Market",
                                Qty = qty,
                                StopPx = targetEntryPrice,
                                OrderLinkId = $"<BOT><STOP-OPEN></BOT>",
                                StopLoss = stopLoss,
                            };
                        }
                        else if (lastPSar.Sar.Value < quoteList.Last().Low && (config.BuyOrSell == 3 || config.BuyOrSell == 2))
                        {
                            targetEntryPrice = Math.Floor(lastPSar.Sar.Value * symbolX) / symbolX;
                            decimal? stopLoss = null;
                            if (param.StopLoss > 0)
                                stopLoss = Math.Ceiling(positionEntryPrice * (1 + param.StopLoss) * symbolX) / symbolX;
                            newStopOpenOrder = new ConditionalRes
                            {
                                Side = "Sell",
                                Symbol = symbol,
                                OrderType = "Market",
                                Qty = qty,
                                StopPx = targetEntryPrice,
                                OrderLinkId = $"<BOT><STOP-OPEN></BOT>",
                                StopLoss = stopLoss,
                            };
                        }
                        else
                        {
                            targetEntryPrice = 0;
                        }
                        LinearListStopOrderResult oldStopOpenOrder = null;
                        List<string> cancelOrderList = new List<string>();
                        foreach (var order in botStopOrderList)
                        {
                            if (order.OrderLinkId.Contains("<STOP-OPEN>"))
                            {
                                oldStopOpenOrder = order;
                                cancelOrderList.Add(order.StopOrderId);
                            }
                        }

                        if (cancelOrderList.Count > 1 || oldStopOpenOrder != null && newStopOpenOrder == null || newStopOpenOrder != null && oldStopOpenOrder != null && oldStopOpenOrder.Side != newStopOpenOrder.Side)
                        {
                            int canceledOrderCount = 0;
                            foreach (var id in cancelOrderList)
                            {
                                apiHelper.CancelActiveStopOrder(symbol, id);
                                canceledOrderCount++;
                            }
                            if (canceledOrderCount > 0)
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} STOP-OPEN orders have been canceled.");
                            oldStopOpenOrder = null;
                        }
                        if (newStopOpenOrder == null)
                        {
                        }
                        else if (oldStopOpenOrder == null)
                        {
                            ConditionalRes resultOrder = apiHelper.NewStopOrder(newStopOpenOrder);
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New STOP-LIMIT order: qty = {qty}, price = {newStopOpenOrder.StopPx}");
                            logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                        }
                        else if (oldStopOpenOrder.Qty != qty || oldStopOpenOrder.TriggerPrice != newStopOpenOrder.StopPx)
                        {
                            ConditionalRes amendOrder = new ConditionalRes
                            {
                                StopOrderId = oldStopOpenOrder.StopOrderId,
                                Symbol = newStopOpenOrder.Symbol,
                                Qty = newStopOpenOrder.Qty,
                                StopPx = newStopOpenOrder.StopPx,
                                StopLoss = newStopOpenOrder.StopLoss,
                            };
                            var resultOrder = apiHelper.AmendStopOrder(amendOrder);
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Amend STOP-LIMIT order: qty = {qty}, price = {newStopOpenOrder.StopPx}");
                            logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                        }
                    }
                    else
                    {
                        List<string> cancelOrderList = new List<string>();
                        int canceledOrderCount = 0;
                        foreach (var order in botStopOrderList)
                        {
                            if (order.OrderLinkId.Contains("<STOP-OPEN>"))
                            {
                                apiHelper.CancelActiveStopOrder(symbol, order.StopOrderId);
                                canceledOrderCount++;
                            }
                        }
                        if (canceledOrderCount > 1)
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} STOP-OPEN orders have been canceled for Invalid position.");
                    }

                endLoop:;
                    //margin = apiHelper.GetMargin(BybitLinearApiHelper.CURRENCY_XBt);
                    //walletBalance = margin.WalletBalance.Value / 100000000m;
                    if (lastWalletBalance != walletBalance)
                    {
                        if (lastWalletBalance != 0)
                        {
                            string logFilename = $"{BybitLinearApiHelper.ServerTime:yyyy-MM}-balance";
                            Logger logger2 = new Logger(logFilename);
                            if (!logger2.ExistFile()) logger2.WriteFile($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}  ({++loopIndex:D6})]    {lastWalletBalance:F8}");
                            string suffix = null;
                            if (positionQty != 0) suffix = $"        position = {positionQty}";
                            logger2.WriteFile($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}  ({++loopIndex:D6})]    {walletBalance:F8}    {walletBalance - lastWalletBalance:F8}    price = {lastPrice:F1}  /  {markPrice:F3}        balance = {(walletBalance - lastWalletBalance) / lastWalletBalance * 100:N4}%{suffix}");
                        }
                        lastWalletBalance = walletBalance;
                    }

                    int waitMilliseconds = (int)(candleList.Last().Timestamp().Value.AddMinutes(param.BinSize) - BybitLinearApiHelper.ServerTime).TotalMilliseconds % 15000;
                    if (waitMilliseconds >= 0)
                    {
                        int waitSeconds = waitMilliseconds / 1000;
                        if (waitSeconds == 0)
                        {
                            waitSeconds = 15;
                            waitMilliseconds = 15000 - waitMilliseconds;
                        }
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Sleeping {waitSeconds:N0} seconds ({apiHelper.RequestCount} requests) ...", ConsoleColor.DarkGray, false);
                        Thread.Sleep(waitMilliseconds);
                        //Logger.WriteWait($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Sleeping {waitSeconds:N0} seconds ({apiHelper.RequestCount} requests) ", waitSeconds, 1);
                        //Thread.Sleep(waitMilliseconds % 1000);
                    }
                    else
                    {
                        logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Error: waitMilliseconds = {waitMilliseconds} < 0", ConsoleColor.Red);
                    }
                }
                catch (Exception ex)
                {
                    if (logger == null) logger = new Logger($"{BybitLinearApiHelper.ServerTime:yyyy-MM-dd}");
                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]    {ex.Message}", ConsoleColor.Red);
                    logger.WriteFile(ex.ToString());
                    logger.WriteFile($"LastPlain4Sign = {BybitLinearApiHelper.LastPlain4Sign}");
                    Thread.Sleep(30000);
                }
                lastLoopTime = DateTime.UtcNow;
            }
        }

    }
}