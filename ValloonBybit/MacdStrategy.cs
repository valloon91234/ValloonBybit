//#define LICENSE_MODE

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using IO.Swagger.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Skender.Stock.Indicators;
using Valloon.Indicators;
using Valloon.Utils;

/**
 * @author Valloon Present
 * @version 2022-07-18
 */
namespace Valloon.Trading
{
    public class MacdStrategy
    {
        private class MacdConfig
        {
            //[JsonProperty("bin_base")]
            //public int BinBase { get; set; } = 60;

            //[JsonProperty("bin_size")]
            //public int BinSize { get; set; } = 4;

            //[JsonProperty("bin_delay")]
            //public int BinDelay { get; set; } = 0;

            [JsonProperty("fast_periods")]
            public int FastPeriods { get; set; } = 12;

            [JsonProperty("slow_periods")]
            public int SlowPeriods { get; set; } = 26;

            [JsonProperty("signal_periods")]
            public int SignalPeriods { get; set; } = 9;

            [JsonProperty("long_close")]
            public decimal LongCloseX { get; set; } = 0.075m;

            [JsonProperty("long_stop")]
            public decimal LongStopX { get; set; } = 0.04m;

            [JsonProperty("short_close")]
            public decimal ShortCloseX { get; set; } = 0.11m;

            [JsonProperty("short_stop")]
            public decimal ShortStopX { get; set; } = 0.03m;

            [JsonProperty("rsi_length")]
            public int RsiLength { get; set; } = 6;

            [JsonProperty("rsi_long_open")]
            public double RsiLongOpen { get; set; } = 80;

            [JsonProperty("rsi_long_close")]
            public double RsiLongClose { get; set; } = 90;

            [JsonProperty("rsi_short_open")]
            public double RsiShortOpen { get; set; } = 26;

            [JsonProperty("rsi_short_close")]
            public double RsiShortClose { get; set; } = 10;
        }

        public void Run()
        {
            int loopIndex = 0;
            DateTime? lastLoopTime = null;
            DateTime? lastCandleTime = null;
            decimal lastWalletBalance = 0;
            string lastParamText = null;
            MacdConfig param = null;
            Logger logger = null;
            DateTime fileModifiedTime = File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location);
            while (true)
            {
                try
                {
                    DateTime currentLoopTime = DateTime.UtcNow;
                    Config config = Config.Load(out bool configUpdated, lastLoopTime != null && lastLoopTime.Value.Day != currentLoopTime.Day);
                    var apiHelper = new BybitLinearApiHelper(config.ApiKey, config.ApiSecret, config.TestnetMode);
                    logger = new Logger($"{BybitLinearApiHelper.ServerTime:yyyy-MM-dd}");
                    if (lastLoopTime == null)
                        logger.WriteLine($"\r\n[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]  MACD bot started.", ConsoleColor.Green);
                    if (configUpdated)
                    {
                        loopIndex = 0;
                        logger.WriteLine($"\r\n[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]  Config loaded.", ConsoleColor.Green);
                        logger.WriteLine(JObject.FromObject(config).ToString(Formatting.Indented));
                        TelegramClient.Init(config);
                        TelegramClient.SendMessageToGroup(JObject.FromObject(config).ToString(Formatting.Indented));
                    }
                    string symbol = config.Symbol.ToUpper();
                    int symbolX = BybitLinearApiHelper.GetX(symbol);
                    if (param == null || BybitLinearApiHelper.ServerTime.Minute == 0 && BybitLinearApiHelper.ServerTime.Second < 3)
                    {
                        string url = $"https://raw.githubusercontent.com/valloon91234/_shared/master/bybit-solusdt-macd-0815.json";
                        string paramText = HttpClient2.HttpGet(url);
                        param = JsonConvert.DeserializeObject<MacdConfig>(paramText);
                        logger.WriteLine($"\r\n[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]  ParamMap loaded.", ConsoleColor.Green);
                        logger.WriteLine(JObject.FromObject(param).ToString(Formatting.Indented));
                        logger.WriteLine();
                        if (lastParamText == null || lastParamText != paramText)
                        {
                            TelegramClient.SendMessageToGroup(JObject.FromObject(param).ToString(Formatting.Indented));
                            lastParamText = paramText;
                        }
                    }
                    else if (configUpdated)
                    {
                        logger.WriteLine();
                    }
                    //var candleList_1h = apiHelper.GetCandleList(symbol, param.BinBase.ToString(), BybitLinearApiHelper.ServerTime.AddMinutes(-60 * 195));
                    //candleList_1h.AddRange(apiHelper.GetCandleList("1h", false, SYMBOL, 1000, null, true, null, binList[binList.Count - 1].Timestamp.Value.AddHours(-1)));
                    //var candleList = BybitLinearApiHelper.ConvertBinSize(candleList_1h, param.BinSize, param.BinDelay);
                    var candleList = apiHelper.GetCandleList(symbol, "240", BybitLinearApiHelper.ServerTime.AddMinutes(-240 * 195));
                    var ticker = apiHelper.GetTicker(symbol);
                    decimal lastPrice = ticker.LastPrice.Value;
                    decimal markPrice = ticker.MarkPrice.Value;
                    var activeOrderList = apiHelper.GetActiveOrders(symbol);
                    var botOrderList = new List<LinearListOrderResult>();
                    foreach (var order in activeOrderList)
                        if (order.OrderLinkId.Contains("<BOT>")) botOrderList.Add(order);
                    var position = apiHelper.GetPositionList(symbol, out var buyPosition, out var sellPosition).First();
                    var usdtBalance = apiHelper.GetWalletBalance("USDT");
                    var walletBalance = usdtBalance._WalletBalance.Value;
                    {
                        //decimal unavailableMarginPercent = 100m * position.OrderMargin.Value / position.WalletBalance.Value;
                        decimal unavailableMarginPercent = 100m * (position.OrderMargin == null ? 1 : (walletBalance - position.OrderMargin.Value) / walletBalance);
                        string balanceChange = null;
                        if (lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        {
                            decimal value = (walletBalance - lastWalletBalance) / lastWalletBalance * 100;
                            if (value >= 0)
                                balanceChange = $"    (+{value:N4} %)";
                            else
                                balanceChange = $"    ({value:N4} %)";
                        }
                        logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss fff}  ({++loopIndex})]    $ {lastPrice:F3} / {markPrice:F3}    {walletBalance:N8}    {activeOrderList.Count} / {botOrderList.Count} / {unavailableMarginPercent:N2} %{balanceChange}", ConsoleColor.White);
                    }
                    decimal positionEntryPrice = 0;
                    decimal positionQty = position.Side == "Buy" ? position.Size.Value : -position.Size.Value;
                    {
                        string consoleTitle = $"$ {lastPrice:N2}  /  {walletBalance:N8}";
                        if (positionQty != 0) consoleTitle += "  /  1 Position";
                        consoleTitle += $"    <{config.Username}>  |   {Config.APP_NAME}  v{fileModifiedTime:yyyy.MM.dd HH:mm:ss}   by  ValloonTrader.com";
                        Console.Title = consoleTitle;
                        if (positionQty != 0)
                        {
                            positionEntryPrice = position.EntryPrice.Value;
                            decimal leverage = 1 / (Math.Abs(positionEntryPrice - position.LiqPrice.Value) / positionEntryPrice) * (1 - (0.0006m * 2));
                            decimal unrealisedPercent = 100m * position.UnrealisedPnl.Value / walletBalance;
                            decimal nowLoss = 100m * (positionEntryPrice - lastPrice) / (position.LiqPrice.Value - positionEntryPrice);
                            logger.WriteLine($"    <Position>    entry = {positionEntryPrice:F2}    qty = {positionQty}    liq = {position.LiqPrice}    leverage = {leverage:F2}    {unrealisedPercent:N2} % / {nowLoss:N2} %", ConsoleColor.Green);
                        }
                    }

                    var quoteList = IndicatorHelper.ToQuote(candleList);
                    var macdList = quoteList.GetMacd(param.FastPeriods, param.SlowPeriods, param.SignalPeriods).ToList();
                    var rsiList = quoteList.GetRsi(param.RsiLength).ToList();

                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  MACD = {macdList[macdList.Count - 4].Histogram:F4} / {macdList[macdList.Count - 3].Histogram:F4} / {macdList[macdList.Count - 2].Histogram:F4} / {macdList[macdList.Count - 1].Histogram:F4} \t RSI = {rsiList[rsiList.Count - 2].Rsi:F4} / {rsiList[rsiList.Count - 1].Rsi:F4}", ConsoleColor.DarkGray);
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
                    if (position.PositionIdx != 0 || position.Mode != "MergedSingle")
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Invalid Position Mode:  PositionIdx = {position.PositionIdx}    Mode = {position.Mode}", ConsoleColor.Red);
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
                        if (config.Exit == 1)
                        {
                            List<string> cancelOrderList = new List<string>();
                            int canceledOrderCount = 0;
                            foreach (var order in botOrderList)
                            {
                                apiHelper.CancelActiveOrder(symbol, order.OrderId);
                                canceledOrderCount++;
                            }
                            if (canceledOrderCount > 0)
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} old orders have been canceled.");
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  No order. (exit = {config.Exit})", ConsoleColor.DarkGray);
                            goto endLoop;
                        }
                        {
                            List<string> cancelOrderList = new List<string>();
                            int canceledOrderCount = 0;
                            if (botOrderList.Count > 1)
                                foreach (var order in botOrderList)
                                {
                                    apiHelper.CancelActiveOrder(symbol, order.OrderId);
                                    canceledOrderCount++;
                                }
                            if (canceledOrderCount > 0)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} old orders have been canceled.");
                                botOrderList = null;
                            }
                        }
                        LinearListOrderResult activeOpenOrder = null;
                        if (botOrderList != null && botOrderList.Count == 1)
                            activeOpenOrder = botOrderList[0];
                        if ((config.BuyOrSell == 1 || config.BuyOrSell == 3) && rsiList[rsiList.Count - 2].Rsi < param.RsiLongOpen && macdList[macdList.Count - 3].Histogram < 0 && macdList[macdList.Count - 2].Histogram >= 0)
                        {
                            if (activeOpenOrder != null && activeOpenOrder.Side != "Buy")
                            {
                                apiHelper.CancelActiveOrder(symbol, activeOpenOrder.OrderId);
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  old open order has been canceled (opposite side).");
                                botOrderList = null;
                            }
                            if (botOrderList == null || botOrderList.Count == 0)
                            {
                                decimal limitPrice = candleList.Last().Open.Value;
                                decimal qty = decimal.Round(walletBalance * config.Leverage * (1 - (0.0006m * 2)) / limitPrice, 1);
                                decimal takeProfitPrice = decimal.Round(limitPrice * (1 + param.LongCloseX), 3);
                                if (takeProfitPrice > lastPrice)
                                {
                                    decimal? stopLossPrice = decimal.Round(limitPrice * (1 - param.LongStopX), 3);
                                    if (stopLossPrice > lastPrice) stopLossPrice = null;
                                    var resultOrder = apiHelper.NewOrder(new OrderRes
                                    {
                                        Side = "Buy",
                                        Symbol = symbol,
                                        OrderType = "Limit",
                                        Qty = qty,
                                        Price = limitPrice,
                                        OrderLinkId = $"<BOT><LONG><OPEN><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                        TakeProfit = takeProfitPrice,
                                        StopLoss = stopLossPrice,
                                        PositionIdx = 0
                                    });
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New LONG-OPEN order: qty = {qty}, price = {limitPrice}, TP = {takeProfitPrice}, SL = {stopLossPrice}");
                                    logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                    TelegramClient.SendMessageToGroup($"<pre>LONG-OPEN: Qty = {qty}  Price = {limitPrice}  TP = {takeProfitPrice}  SL = {stopLossPrice}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                            }
                        }
                        else if ((config.BuyOrSell == 2 || config.BuyOrSell == 3) && rsiList[rsiList.Count - 2].Rsi > param.RsiShortOpen && macdList[macdList.Count - 3].Histogram > 0 && macdList[macdList.Count - 2].Histogram <= 0)
                        {
                            if (activeOpenOrder != null && activeOpenOrder.Side != "Sell")
                            {
                                apiHelper.CancelActiveOrder(symbol, activeOpenOrder.OrderId);
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  old open order has been canceled (opposite side).");
                                botOrderList = null;
                            }
                            if (botOrderList == null || botOrderList.Count == 0)
                            {
                                decimal limitPrice = candleList.Last().Open.Value;
                                decimal qty = decimal.Round(walletBalance * config.Leverage * (1 - (0.0006m * 2)) / limitPrice, 1);
                                decimal takeProfitPrice = decimal.Round(limitPrice * (1 - param.ShortCloseX), 3);
                                if (takeProfitPrice < lastPrice)
                                {
                                    decimal? stopLossPrice = decimal.Round(limitPrice * (1 + param.ShortStopX), 3);
                                    if (stopLossPrice < lastPrice) stopLossPrice = null;
                                    var resultOrder = apiHelper.NewOrder(new OrderRes
                                    {
                                        Side = "Sell",
                                        Symbol = symbol,
                                        OrderType = "Limit",
                                        Qty = qty,
                                        Price = limitPrice,
                                        OrderLinkId = $"<BOT><SHORT><OPEN><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                        TakeProfit = takeProfitPrice,
                                        StopLoss = stopLossPrice,
                                        PositionIdx = 0
                                    });
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New SHORT-OPEN order: qty = {qty}, price = {limitPrice}, TP = {takeProfitPrice}, SL = {stopLossPrice}");
                                    logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                    TelegramClient.SendMessageToGroup($"<pre>SHORT-OPEN: Qty = {qty}  Price = {limitPrice}  TP = {takeProfitPrice}  SL = {stopLossPrice}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                            }
                        }
                        else if (activeOpenOrder != null && (BybitLinearApiHelper.ServerTime - DateTime.ParseExact(activeOpenOrder.CreatedTime, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture)).TotalMinutes > 240)
                        {
                            apiHelper.CancelActiveOrder(symbol, activeOpenOrder.OrderId);
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  old open order has been canceled (timeout).");
                            TelegramClient.SendMessageToGroup($"<pre>Old open order has been canceled (timeout).</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                            botOrderList = null;
                        }
                    }
                    else
                    {
                        //List<string> cancelOrderList = new List<string>();
                        //int canceledOrderCount = 0;
                        //foreach (var order in botOrderList)
                        //{
                        //    apiHelper.CancelActiveOrder(symbol, order.OrderId);
                        //    canceledOrderCount++;
                        //}
                        //if (canceledOrderCount > 0)
                        //{
                        //    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} old orders have been canceled.");
                        //    botOrderList = null;
                        //}
                        if (positionQty > 0 && macdList[macdList.Count - 3].Histogram >= 0 && macdList[macdList.Count - 2].Histogram < 0)
                        {
                            var resultOrder = apiHelper.NewOrder(new OrderRes
                            {
                                Side = "Sell",
                                Symbol = symbol,
                                OrderType = "Market",
                                Qty = position.Size.Value,
                                CloseOnTrigger = true,
                                PositionIdx = 0
                            });
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Long position closed by market.");
                            logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                            TelegramClient.SendMessageToGroup($"<pre>Long position closed by market.</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                        }
                        else if (positionQty < 0 && macdList[macdList.Count - 2].Histogram <= 0 && macdList[macdList.Count - 2].Histogram > 0)
                        {
                            var resultOrder = apiHelper.NewOrder(new OrderRes
                            {
                                Side = "Buy",
                                Symbol = symbol,
                                OrderType = "Market",
                                Qty = position.Size.Value,
                                CloseOnTrigger = true,
                                PositionIdx = 0
                            });
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Short position closed by market.");
                            logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                            TelegramClient.SendMessageToGroup($"<pre>Short position closed by market.</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                        }
                        LinearListOrderResult botCloseOrder = null;
                        if (botOrderList != null)
                            foreach (var order in botOrderList)
                            {
                                if (order.OrderLinkId.Contains("<CLOSE>"))
                                {
                                    botCloseOrder = order;
                                    break;
                                }
                            }
                        LinearListOrderResult manualCloseOrder = null;
                        foreach (var order in activeOrderList)
                        {
                            if (order.CloseOnTrigger != null && order.CloseOnTrigger.Value)
                            {
                                manualCloseOrder = order;
                                break;
                            }
                        }
                        if (manualCloseOrder == null)
                            if (positionQty > 0)
                            {
                                var rsi = rsiList.Last().Rsi.Value;
                                var quoteList2 = new List<Quote>(quoteList);
                                quoteList2.Last().Close = quoteList2.Last().Open;
                                while (rsi < param.RsiLongClose)
                                {
                                    quoteList2.Last().Close += .005m;
                                    rsi = quoteList2.GetRsi(param.RsiLength).Last().Rsi.Value;
                                }
                                var price = quoteList2.Last().Close;
                                if (botCloseOrder == null)
                                {
                                    var resultOrder = apiHelper.NewOrder(new OrderRes
                                    {
                                        Side = "Sell",
                                        Symbol = symbol,
                                        OrderType = "Limit",
                                        Qty = positionQty,
                                        Price = price,
                                        CloseOnTrigger = true,
                                        OrderLinkId = $"<BOT><CLOSE><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                        PositionIdx = 0
                                    });
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New CLOSE order: price = {price}");
                                    logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                    TelegramClient.SendMessageToGroup($"<pre>New Close Order: Price = {price}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                                else if (botCloseOrder.Price != price || botCloseOrder.Qty != positionQty)
                                {
                                    var resultOrder = apiHelper.AmendOrder(new OrderRes
                                    {
                                        OrderId = botCloseOrder.OrderId,
                                        Symbol = symbol,
                                        Qty = positionQty,
                                        Price = price,
                                        OrderLinkId = $"<BOT><CLOSE><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    });
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Amend CLOSE order: price = {price}");
                                    logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                    TelegramClient.SendMessageToGroup($"<pre>Amend Close Order: Price = {price}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                            }
                            else if (positionQty < 0)
                            {
                                var rsi = rsiList.Last().Rsi.Value;
                                var quoteList2 = new List<Quote>(quoteList);
                                quoteList2.Last().Close = quoteList2.Last().Open;
                                while (rsi > param.RsiShortClose)
                                {
                                    quoteList2.Last().Close -= .005m;
                                    rsi = quoteList2.GetRsi(param.RsiLength).Last().Rsi.Value;
                                }
                                var price = quoteList2.Last().Close;
                                if (botCloseOrder == null)
                                {
                                    var resultOrder = apiHelper.NewOrder(new OrderRes
                                    {
                                        Side = "Buy",
                                        Symbol = symbol,
                                        OrderType = "Limit",
                                        Qty = -positionQty,
                                        Price = price,
                                        CloseOnTrigger = true,
                                        OrderLinkId = $"<BOT><CLOSE><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                        PositionIdx = 0
                                    });
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New CLOSE order: price = {price}");
                                    logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                    TelegramClient.SendMessageToGroup($"<pre>New Close Order: Price = {price}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                                else if (botCloseOrder.Price != price || botCloseOrder.Qty != -positionQty)
                                {
                                    var resultOrder = apiHelper.AmendOrder(new OrderRes
                                    {
                                        OrderId = botCloseOrder.OrderId,
                                        Symbol = symbol,
                                        Qty = -positionQty,
                                        Price = price,
                                        OrderLinkId = $"<BOT><CLOSE><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    });
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Amend CLOSE order: price = {price}");
                                    logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                    TelegramClient.SendMessageToGroup($"<pre>Amend Close Order: Price = {price}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                            }
                    }

                endLoop:;
                    if (loopIndex == 1 || BybitLinearApiHelper.ServerTime.Minute % 30 == 0 || lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                    {
                        decimal unavailableMarginPercent = 100m * (position.OrderMargin == null ? 1 : (walletBalance - position.OrderMargin.Value) / walletBalance);
                        string balanceChange = null;
                        if (lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        {
                            decimal value = (walletBalance - lastWalletBalance) / lastWalletBalance * 100;
                            if (value >= 0)
                                balanceChange = $"    (+{value:N4} %)";
                            else
                                balanceChange = $"    ({value:N4} %)";
                        }
                        string text = $"$ {lastPrice:F3} / {markPrice:F3}    {walletBalance:N8}    {activeOrderList.Count} / {(botOrderList == null ? 0 : botOrderList.Count)} / {unavailableMarginPercent:N2} %";
                        if (positionQty != 0)
                        {
                            decimal leverage = 1 / (Math.Abs(positionEntryPrice - position.LiqPrice.Value) / positionEntryPrice);
                            decimal unrealisedPercent = 100m * position.UnrealisedPnl.Value / walletBalance;
                            string percent = unrealisedPercent.ToString("N2");
                            if (unrealisedPercent > 0) percent = "+" + percent;
                            text += $"\n<pre>E = {positionEntryPrice:F2}  Liq = {position.LiqPrice}  X = {leverage:F2}   {percent} %</pre>";
                        }

                        var quoteList2 = new List<Quote>(quoteList);
                        quoteList2.Add(quoteList2.Last());
                        double forcastMacd = quoteList2.GetMacd(param.FastPeriods, param.SlowPeriods, param.SignalPeriods).Last().Histogram.Value;

                        text += $"\nMACD =  {macdList[macdList.Count - 4].Histogram:F4}    {macdList[macdList.Count - 3].Histogram:F4}    {macdList[macdList.Count - 2].Histogram:F4}    {macdList[macdList.Count - 1].Histogram:F4}    ({forcastMacd:F4})\nRSI =  {rsiList[rsiList.Count - 3].Rsi:F2}    {rsiList[rsiList.Count - 2].Rsi:F2}    {rsiList[rsiList.Count - 1].Rsi:F2}";
                        text += $"\n<pre>[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}  ({loopIndex})]  {balanceChange}</pre>";
                        TelegramClient.SendMessageToGroup(text, Telegram.Bot.Types.Enums.ParseMode.Html);
                    }
                    if (lastCandleTime == null || lastCandleTime.Value != quoteList.Last().Date)
                    {
                        var macd = macdList.Last().Histogram.Value;
                        var quoteList2 = new List<Quote>(quoteList);
                        if (macd > 0)
                        {
                            while (macd > 0)
                            {
                                quoteList2.Last().Close -= .005m;
                                macd = quoteList2.GetMacd(param.FastPeriods, param.SlowPeriods, param.SignalPeriods).Last().Histogram.Value;
                            }
                            var text = $"<pre>Target = {quoteList2.Last().Close}</pre>";
                            TelegramClient.SendMessageToGroup(text, Telegram.Bot.Types.Enums.ParseMode.Html);
                        }
                        else if (macd < 0)
                        {
                            while (macd < 0)
                            {
                                quoteList2.Last().Close += .005m;
                                macd = quoteList2.GetMacd(param.FastPeriods, param.SlowPeriods, param.SignalPeriods).Last().Histogram.Value;
                            }
                            var text = $"<pre>Target = {quoteList2.Last().Close}</pre>";
                            TelegramClient.SendMessageToGroup(text, Telegram.Bot.Types.Enums.ParseMode.Html);
                        }
                    }
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
                    lastCandleTime = quoteList.Last().Date;

                    DateTime now = BybitLinearApiHelper.ServerTime;
                    TimeSpan d = new TimeSpan(1, 0, 0);
                    int waitMilliseconds = (int)(new DateTime((now.Ticks + d.Ticks - 1) / d.Ticks * d.Ticks, now.Kind) - now).TotalMilliseconds % 300000 + 1000;
                    if (waitMilliseconds >= 0)
                    {
                        int waitSeconds = waitMilliseconds / 1000;
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Sleeping {waitSeconds:N0} seconds ({apiHelper.RequestCount} requests) ...", ConsoleColor.DarkGray, false);
                        Thread.Sleep(waitMilliseconds);
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
                catch (Exception ex)
                {
                    if (logger == null) logger = new Logger($"{BybitLinearApiHelper.ServerTime:yyyy-MM-dd}");
                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]    {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                    logger.WriteFile(ex.ToString());
                    logger.WriteFile($"LastPlain4Sign = {BybitLinearApiHelper.LastPlain4Sign}");
                    TelegramClient.SendMessageToGroup(ex.ToString());
                    Thread.Sleep(30000);
                }
                lastLoopTime = DateTime.UtcNow;
            }
        }

    }
}