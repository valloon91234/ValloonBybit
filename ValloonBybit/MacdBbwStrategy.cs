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
 * https://www.bybit.com/en-US/help-center/bybitHC_Article?language=en_US&id=000001063
 */
namespace Valloon.Trading
{
    public class MacdBbwStrategy
    {
        private class MacdConfig
        {
            [JsonProperty("fast_periods")]
            public int FastPeriods { get; set; } = 12;

            [JsonProperty("slow_periods")]
            public int SlowPeriods { get; set; } = 26;

            [JsonProperty("signal_periods")]
            public int SignalPeriods { get; set; } = 9;

            [JsonProperty("long_bbw_length")]
            public int LongBbwLength { get; set; } = 8;

            [JsonProperty("long_bbw_open")]
            public double LongBbwOpen { get; set; } = 0.088;

            [JsonProperty("long_bbw_close")]
            public double LongBbwClose { get; set; } = 0.085;

            [JsonProperty("long_close")]
            public decimal LongCloseX { get; set; } = 0.06m;

            [JsonProperty("long_stop")]
            public decimal LongStopX { get; set; } = 0.01m;

            [JsonProperty("short_bbw_length")]
            public int ShortBbwLength { get; set; } = 4;

            [JsonProperty("short_bbw_open")]
            public double ShortBbwOpen { get; set; } = 0.058;

            [JsonProperty("short_bbw_close")]
            public double ShortBbwClose { get; set; } = 0;

            [JsonProperty("short_close")]
            public decimal ShortCloseX { get; set; } = 0.07m;

            [JsonProperty("short_stop")]
            public decimal ShortStopX { get; set; } = 0.01m;

            [JsonProperty("rsi_length")]
            public int RsiLength { get; set; } = 14;

            [JsonProperty("rsi_long_open")]
            public double RsiLongOpen { get; set; } = 69;

            [JsonProperty("rsi_long_close")]
            public double RsiLongClose { get; set; } = 90;

            [JsonProperty("rsi_short_open")]
            public double RsiShortOpen { get; set; } = 30;

            [JsonProperty("rsi_short_close")]
            public double RsiShortClose { get; set; } = 7.5;
        }

        public static string LastMessage;

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
                        TelegramClient.SendMessageToBroadcastGroup(JObject.FromObject(config).ToString(Formatting.Indented));
                    }
                    string symbol = config.Symbol.ToUpper();
                    int symbolX = BybitLinearApiHelper.GetX(symbol);
                    if (param == null || BybitLinearApiHelper.ServerTime.Minute == 0 && BybitLinearApiHelper.ServerTime.Second < 3)
                    {
                        string url = $"https://raw.githubusercontent.com/valloon91234/_shared/master/bybit-solusdt-macd-0905.json";
                        string paramText = HttpClient2.HttpGet(url);
                        param = JsonConvert.DeserializeObject<MacdConfig>(paramText);
                        logger.WriteLine($"\r\n[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]  ParamMap loaded.", ConsoleColor.Green);
                        logger.WriteLine(JObject.FromObject(param).ToString(Formatting.Indented));
                        logger.WriteLine();
                        if (lastParamText == null || lastParamText != paramText)
                        {
                            TelegramClient.SendMessageToBroadcastGroup(JObject.FromObject(param).ToString(Formatting.Indented));
                            lastParamText = paramText;
                        }
                    }
                    else if (configUpdated)
                    {
                        logger.WriteLine();
                    }
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
                        decimal unavailableMarginPercent = 100m * (position.OrderMargin == null ? 1 : (walletBalance - position.OrderMargin.Value) / walletBalance);
                        string balanceChange = null;
                        if (lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        {
                            decimal value = (walletBalance - lastWalletBalance) / lastWalletBalance * 100;
                            if (value >= 0)
                                balanceChange = $"   (+{value:N4} %)";
                            else
                                balanceChange = $"   ({value:N4} %)";
                        }
                        logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss fff}  ({++loopIndex})]   $ {lastPrice:F3} / {markPrice:F3}   {walletBalance:N8}   {activeOrderList.Count} / {botOrderList.Count} / {unavailableMarginPercent:N2} %{balanceChange}", ConsoleColor.White);
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
                            decimal leverage = 1 / (Math.Abs(positionEntryPrice - position.BustPrice.Value) / positionEntryPrice) * (1 - (0.0006m * 2));
                            //decimal nowLoss = 100m * (positionEntryPrice - lastPrice) / (position.LiqPrice.Value - positionEntryPrice);
                            decimal unrealisedPnl = position.UnrealisedPnl.Value;
                            decimal unrealisedPercent = 100m * unrealisedPnl / walletBalance;
                            string pnlText;
                            if (unrealisedPnl > 0)
                                pnlText = $"+{unrealisedPnl} (+{unrealisedPercent:N2} %)";
                            else
                                pnlText = $"{unrealisedPnl} ({unrealisedPercent:N2} %)";
                            logger.WriteLine($"    Entry = {positionEntryPrice}   qty = {positionQty}   liq = {position.LiqPrice}   lv = {leverage:F2}   TP = {(position.TakeProfit == null ? "None" : position.TakeProfit.ToString())}   SL = {(position.StopLoss == null ? "None" : position.StopLoss.ToString())}   P&L = {pnlText}", ConsoleColor.Green);
                        }
                    }
                    bool isNewCandle = lastCandleTime != null && lastCandleTime.Value != candleList.Last().Timestamp().Value;

                    var quoteList = IndicatorHelper.ToQuote(candleList);
                    var macdList = quoteList.GetMacd(param.FastPeriods, param.SlowPeriods, param.SignalPeriods).ToList();
                    var rsiList = quoteList.GetRsi(param.RsiLength).ToList();
                    var longBbwList = quoteList.GetBollingerBands(param.LongBbwLength, 2).ToList();
                    var shortBbwList = quoteList.GetBollingerBands(param.ShortBbwLength, 2).ToList();

                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  MACD = {macdList[macdList.Count - 3].Histogram:F4} / {macdList[macdList.Count - 2].Histogram:F4} / {macdList[macdList.Count - 1].Histogram:F4} \t RSI = {rsiList[rsiList.Count - 2].Rsi:F4} / {rsiList[rsiList.Count - 1].Rsi:F4}", ConsoleColor.DarkGray);
                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  BBW({param.LongBbwLength}) = {longBbwList[longBbwList.Count - 3].Width:F4} / {longBbwList[longBbwList.Count - 2].Width:F4} / {longBbwList.Last().Width:F4} \t BBW({param.ShortBbwLength}) = {shortBbwList[shortBbwList.Count - 3].Width:F4} / {shortBbwList[shortBbwList.Count - 2].Width:F4} / {shortBbwList.Last().Width:F4}", ConsoleColor.DarkGray);
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

                    if (positionQty != 0)
                    {
                        if (isNewCandle && positionQty > 0 && (macdList[macdList.Count - 2].Histogram < 0 || longBbwList[longBbwList.Count - 3].Width >= param.LongBbwClose && longBbwList[longBbwList.Count - 2].Width < param.LongBbwClose))
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
                            TelegramClient.SendMessageToBroadcastGroup($"<pre>Long position closed by market.</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                            positionQty = 0;
                        }
                        else if (isNewCandle && positionQty < 0 && (macdList[macdList.Count - 2].Histogram > 0 || shortBbwList[shortBbwList.Count - 3].Width >= param.ShortBbwClose && shortBbwList[shortBbwList.Count - 2].Width < param.ShortBbwClose))
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
                            TelegramClient.SendMessageToBroadcastGroup($"<pre>Short position closed by market.</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                            positionQty = 0;
                        }
                        else
                        {
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
                                if (order.OrderLinkId.Contains("<BOT>")) continue;
                                if (order.CloseOnTrigger != null && order.CloseOnTrigger.Value)
                                {
                                    manualCloseOrder = order;
                                    break;
                                }
                            }
                            if (manualCloseOrder == null)
                            {
                                var rsi = rsiList.Last().Rsi.Value;
                                var quoteList2 = new List<Quote>(quoteList);
                                quoteList2[quoteList2.Count - 1] = new Quote
                                {
                                    Date = quoteList.Last().Date,
                                    Open = quoteList.Last().Open,
                                    High = quoteList.Last().High,
                                    Low = quoteList.Last().Low,
                                    Close = quoteList.Last().Close,
                                    Volume = quoteList.Last().Volume
                                };
                                quoteList2.Last().Close = quoteList2.Last().Open;
                                if (positionQty > 0)
                                {
                                    while (rsi < param.RsiLongClose)
                                    {
                                        quoteList2.Last().Close += .005m;
                                        rsi = quoteList2.GetRsi(param.RsiLength).Last().Rsi.Value;
                                    }
                                    var price = quoteList2.Last().Close;
                                    if (price > lastPrice * 1.2m)
                                    {
                                        if (botCloseOrder != null)
                                        {
                                            apiHelper.CancelActiveOrder(symbol, botCloseOrder.OrderId);
                                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  old close order has been canceled (too high).");
                                        }
                                    }
                                    else if (botCloseOrder == null)
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
                                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New CLOSE order:  qty = {positionQty}  price = {price}");
                                        logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                        TelegramClient.SendMessageToBroadcastGroup($"<pre>New Close Order: Price = {price}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
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
                                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Amend CLOSE order:  qty = {positionQty}  price = {price}");
                                        logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                        TelegramClient.SendMessageToBroadcastGroup($"<pre>Amend Close Order: Price = {price}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                                    }
                                }
                                else if (positionQty < 0)
                                {
                                    while (rsi > param.RsiShortClose)
                                    {
                                        quoteList2.Last().Close -= .005m;
                                        rsi = quoteList2.GetRsi(param.RsiLength).Last().Rsi.Value;
                                    }
                                    var price = quoteList2.Last().Close;
                                    if (lastPrice > price * 1.2m)
                                    {
                                        if (botCloseOrder != null)
                                        {
                                            apiHelper.CancelActiveOrder(symbol, botCloseOrder.OrderId);
                                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  old close order has been canceled (too low).");
                                        }
                                    }
                                    else if (botCloseOrder == null)
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
                                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New CLOSE order:  qty = {-positionQty}  price = {price}");
                                        logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                        TelegramClient.SendMessageToBroadcastGroup($"<pre>New Close Order: Price = {price}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
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
                                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Amend CLOSE order:  qty = {-positionQty}  price = {price}");
                                        logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                        TelegramClient.SendMessageToBroadcastGroup($"<pre>Amend Close Order: Price = {price}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                                    }
                                }
                            }
                        }
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

                        if (isNewCandle && (config.BuyOrSell == 1 || config.BuyOrSell == 3) && rsiList[rsiList.Count - 2].Rsi < param.RsiLongOpen
                            && macdList[macdList.Count - 2].Histogram > 0 && macdList[macdList.Count - 2].Histogram > macdList[macdList.Count - 3].Histogram
                            && longBbwList[longBbwList.Count - 3].Width < param.LongBbwOpen && longBbwList[longBbwList.Count - 2].Width >= param.LongBbwOpen)
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
                                    TelegramClient.SendMessageToBroadcastGroup($"<pre>LONG-OPEN: Qty = {qty}  Price = {limitPrice}  TP = {takeProfitPrice}  SL = {stopLossPrice}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                            }
                        }
                        else if (isNewCandle && (config.BuyOrSell == 2 || config.BuyOrSell == 3) && rsiList[rsiList.Count - 2].Rsi > param.RsiShortOpen
                            && macdList[macdList.Count - 2].Histogram < 0 && macdList[macdList.Count - 2].Histogram < macdList[macdList.Count - 3].Histogram
                            && shortBbwList[shortBbwList.Count - 3].Width < param.ShortBbwOpen && shortBbwList[shortBbwList.Count - 2].Width >= param.ShortBbwOpen)
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
                                    TelegramClient.SendMessageToBroadcastGroup($"<pre>SHORT-OPEN: Qty = {qty}  Price = {limitPrice}  TP = {takeProfitPrice}  SL = {stopLossPrice}</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                            }
                        }
                        else if (activeOpenOrder != null && (BybitLinearApiHelper.ServerTime - DateTime.ParseExact(activeOpenOrder.CreatedTime, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture)).TotalMinutes > 240)
                        {
                            apiHelper.CancelActiveOrder(symbol, activeOpenOrder.OrderId);
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  old open order has been canceled (timeout).");
                            TelegramClient.SendMessageToBroadcastGroup($"<pre>Old open order has been canceled (timeout).</pre>", Telegram.Bot.Types.Enums.ParseMode.Html);
                            botOrderList = null;
                        }
                    }

                endLoop:;
                    {
                        {
                            var macd = macdList.Last().Histogram.Value;
                            var quoteList2 = new List<Quote>(quoteList);
                            quoteList2[quoteList2.Count - 1] = new Quote
                            {
                                Date = quoteList.Last().Date,
                                Open = quoteList.Last().Open,
                                High = quoteList.Last().High,
                                Low = quoteList.Last().Low,
                                Close = quoteList.Last().Close,
                                Volume = quoteList.Last().Volume
                            };
                        }

                        //decimal unavailableMarginPercent = 100m * (position.OrderMargin == null ? 1 : (walletBalance - position.OrderMargin.Value) / walletBalance);
                        string balanceChange = null;
                        if (lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        {
                            decimal value = (walletBalance - lastWalletBalance) / lastWalletBalance * 100;
                            if (value >= 0)
                                balanceChange = $"   +{(walletBalance - lastWalletBalance)}  (+{value:N2} %)";
                            else
                                balanceChange = $"   {(walletBalance - lastWalletBalance)}  ({value:N2} %)";
                        }
                        string text = $"$ {lastPrice:F3} / {markPrice:F3}    {walletBalance:N8}    {activeOrderList.Count} / {(botOrderList == null ? 0 : botOrderList.Count)}";
                        if (positionQty != 0)
                        {
                            decimal leverage = 1 / (Math.Abs(positionEntryPrice - position.BustPrice.Value) / positionEntryPrice);
                            decimal unrealisedPnl = position.UnrealisedPnl.Value;
                            decimal unrealisedPercent = 100m * unrealisedPnl / walletBalance;
                            string pnlText;
                            if (unrealisedPnl > 0)
                                pnlText = $"+{unrealisedPnl}  (+{unrealisedPercent:N2} %)";
                            else
                                pnlText = $"{unrealisedPnl}  ({unrealisedPercent:N2} %)";
                            text += $"\n<pre>Entry = {positionEntryPrice}   Qty = {positionQty}   Liq = {position.LiqPrice}   Lv = {leverage:F2}\nTP = {(position.TakeProfit == null ? "None" : position.TakeProfit.ToString())}   SL = {(position.StopLoss == null ? "None" : position.StopLoss.ToString())}   P&L = {pnlText}</pre>";
                        }

                        double forcastMacd, forcastMacd2, forcastMacd3;
                        {
                            var quoteList2 = new List<Quote>(quoteList);
                            quoteList2.Add(quoteList2.Last());
                            forcastMacd = quoteList2.GetMacd(param.FastPeriods, param.SlowPeriods, param.SignalPeriods).Last().Histogram.Value;
                            quoteList2.Add(quoteList2.Last());
                            forcastMacd2 = quoteList2.GetMacd(param.FastPeriods, param.SlowPeriods, param.SignalPeriods).Last().Histogram.Value;
                            quoteList2.Add(quoteList2.Last());
                            forcastMacd3 = quoteList2.GetMacd(param.FastPeriods, param.SlowPeriods, param.SignalPeriods).Last().Histogram.Value;
                        }

                        text += $"\nMACD =  {macdList[macdList.Count - 4].Histogram:F4}    {macdList[macdList.Count - 3].Histogram:F4}    {macdList[macdList.Count - 2].Histogram:F4}    {macdList[macdList.Count - 1].Histogram:F4}    ({forcastMacd:F2}   {forcastMacd2:F2}   {forcastMacd3:F2})";
                        text += $"\nRSI ({param.RsiLength}) =  {rsiList[rsiList.Count - 4].Rsi:F4}    {rsiList[rsiList.Count - 3].Rsi:F4}    {rsiList[rsiList.Count - 2].Rsi:F4}    {rsiList[rsiList.Count - 1].Rsi:F4}";
                        text += $"\nBBW ({param.LongBbwLength}) =  {longBbwList[longBbwList.Count - 4].Width:F4}    {longBbwList[longBbwList.Count - 3].Width:F4}    {longBbwList[longBbwList.Count - 2].Width:F4}    {longBbwList[longBbwList.Count - 1].Width:F4}";
                        text += $"\nBBW ({param.ShortBbwLength}) =  {shortBbwList[shortBbwList.Count - 4].Width:F4}    {shortBbwList[shortBbwList.Count - 3].Width:F4}    {shortBbwList[shortBbwList.Count - 2].Width:F4}    {shortBbwList[shortBbwList.Count - 1].Width:F4}";
                        string prefix = null;
                        if (isNewCandle)
                            prefix = "4H ";
                        text += $"\n<pre>{prefix}[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]{balanceChange}</pre>";
                        if (loopIndex == 1 || BybitLinearApiHelper.ServerTime.Minute % 30 == 0 || lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                            TelegramClient.SendMessageToBroadcastGroup(text, Telegram.Bot.Types.Enums.ParseMode.Html);
                        //if (isNewCandle || lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        //    TelegramClient.SendMessageToListenGroup(text, Telegram.Bot.Types.Enums.ParseMode.Html);
                        LastMessage = text;
                    }

                    usdtBalance = apiHelper.GetWalletBalance("USDT");
                    walletBalance = usdtBalance._WalletBalance.Value;
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
                    TelegramClient.SendMessageToBroadcastGroup(ex.ToString());
                    Thread.Sleep(30000);
                }
                lastLoopTime = DateTime.UtcNow;
            }
        }

    }
}