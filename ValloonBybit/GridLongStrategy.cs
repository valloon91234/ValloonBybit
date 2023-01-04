//#define LICENSE_MODE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using IniParser.Model;
using IniParser;
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
 * https://github.com/rickyah/ini-parser
 */
namespace Valloon.Trading
{
    public class GridLongStrategy
    {
        private class GridConfig
        {
            [JsonProperty("symbol")]
            public string Symbol { get; set; }

            [JsonProperty("price_decimals")]
            public int PriceDecimals { get; set; }

            [JsonProperty("qty_decimals")]
            public int QtyDecimals { get; set; }

            [JsonProperty("qty_max_limit")]
            public int QtyMaxLimit { get; set; }

            [JsonProperty("min_price")]
            public decimal MinPrice { get; set; }

            [JsonProperty("max_price")]
            public decimal MaxPrice { get; set; }

            [JsonProperty("order_qty")]
            public decimal OrderQty { get; set; }

            [JsonProperty("order_step")]
            public decimal OrderStep { get; set; }

            [JsonProperty("order_pinned")]
            public decimal OrderPinned { get; set; }

            [JsonProperty("reset")]
            public decimal ResetX { get; set; }

            [JsonProperty("stop_loss")]
            public decimal StopLoss { get; set; }

            [JsonProperty("stop_loss_reset")]
            public decimal StopLossReset { get; set; }

            [JsonProperty("interval")]
            public int Interval { get; set; }
        }

        public static readonly string INI_FILENAME = "_grid_save.ini";
        public static string LastMessage;

        public void Run()
        {
            int loopIndex = 0;
            DateTime? lastLoopTime = null;
            decimal? lastPositionQty = null;
            decimal lastWalletBalance = 0;
            string lastParamJson = null;
            GridConfig param = null;
            Logger logger = null;
            DateTime fileModifiedTime = File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location);
            var iniDataParser = new FileIniDataParser();
            if (!File.Exists(INI_FILENAME))
            {
                File.WriteAllText(INI_FILENAME, "[GRID]");
            }
            while (true)
            {
                var interval = 30;
                string telegramMessage = null;
                try
                {
                    var iniData = iniDataParser.ReadFile(INI_FILENAME);
                    decimal startPrice = 0;
                    try
                    {
                        startPrice = decimal.Parse(iniData["GRID"]["START"]);
                    }
                    catch { }
                    DateTime currentLoopTime = DateTime.UtcNow;
                    Config config = Config.Load(out bool configUpdated, lastLoopTime != null && lastLoopTime.Value.Day != currentLoopTime.Day);
                    var apiHelper = new BybitLinearApiHelper(config.ApiKey, config.ApiSecret, config.TestnetMode);
                    logger = new Logger($"{BybitLinearApiHelper.ServerTime:yyyy-MM-dd}");
                    if (lastLoopTime == null)
                        logger.WriteLine($"\r\n[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]  MM bot started.", ConsoleColor.Green);
                    if (configUpdated)
                    {
                        loopIndex = 0;
                        logger.WriteLine($"\r\n[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]  Config loaded.", ConsoleColor.Green);
                        logger.WriteLine(JObject.FromObject(config).ToString(Formatting.Indented));
                        if (!Debugger.IsAttached)
                        {
                            TelegramClient.Init(config);
                            TelegramClient.SendMessageToAdmin(JObject.FromObject(config).ToString(Formatting.Indented));
                        }
                    }
                    string paramJson = File.ReadAllText("config-grid.json");
                    if (param == null || paramJson != lastParamJson)
                    {
                        param = JsonConvert.DeserializeObject<GridConfig>(paramJson);
                        logger.WriteLine($"\r\n[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]  ParamMap loaded.", ConsoleColor.Green);
                        logger.WriteLine(JObject.FromObject(param).ToString(Formatting.Indented));
                        logger.WriteLine();
                        TelegramClient.SendMessageToAdmin(JObject.FromObject(param).ToString(Formatting.Indented));
                        lastParamJson = paramJson;
                    }
                    else if (configUpdated)
                    {
                        logger.WriteLine();
                    }
                    interval = param.Interval;
                    var symbol = param.Symbol;
                    var ticker = apiHelper.GetTicker(symbol);
                    decimal lastPrice = ticker.LastPrice.Value;
                    decimal markPrice = ticker.MarkPrice.Value;
                    var activeOrderList = apiHelper.GetActiveOrders(symbol);
                    var botUpperOrderList = new List<LinearListOrderResult>();
                    var botLowerOrderList = new List<LinearListOrderResult>();
                    foreach (var order in activeOrderList)
                    {
                        if (!order.OrderLinkId.Contains("<BOT>")) continue;
                        if (order.Side == "Sell")
                            botUpperOrderList.Add(order);
                        else if (order.Side == "Buy")
                            botLowerOrderList.Add(order);
                    }
                    var position = apiHelper.GetPositionList(symbol, out var buyPosition, out var sellPosition).First();
                    var usdtBalance = apiHelper.GetWalletBalance("USDT");
                    var walletBalance = usdtBalance._WalletBalance.Value;
                    var availableBalance = usdtBalance.AvailableBalance;
                    {
                        decimal unavailableMarginPercent = 100m * (position.OrderMargin == null ? 1 : (walletBalance - position.OrderMargin.Value) / walletBalance);
                        string balanceChange = null;
                        if (lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        {
                            decimal value = (walletBalance - lastWalletBalance) / lastWalletBalance * 100;
                            if (value >= 0)
                                balanceChange = $"   (+{value:N2} %)";
                            else
                                balanceChange = $"   ({value:N2} %)";
                        }
                        logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss fff}  ({++loopIndex})]   $ {lastPrice:F4} / {markPrice:F4}   {walletBalance:N4} / {availableBalance:N4}   {activeOrderList.Count} / {botUpperOrderList.Count} / {botLowerOrderList.Count} / {unavailableMarginPercent:N2} %{balanceChange}", ConsoleColor.White);
                    }
                    decimal positionEntryPrice = 0;
                    decimal positionQty = position.Side == "Buy" ? position.Size.Value : -position.Size.Value;
                    {
                        string consoleTitle = $"$ {lastPrice:N2}  /  {walletBalance}";
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
                    if (config.Exit == 2)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  No order. (exit = {config.Exit})", ConsoleColor.DarkGray);
                        interval = 30;
                        goto endLoop;
                    }
                    if (position.PositionIdx != 0 || position.Mode != "MergedSingle" || position.IsIsolated == null || !position.IsIsolated.Value)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Invalid Position Mode:  PositionIdx = {position.PositionIdx}    Mode = {position.Mode}    IsIsolated = {position.IsIsolated}", ConsoleColor.Red);
                        interval = 30;
                        goto endLoop;
                    }
                    if (positionQty < 0)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Short position exists:  Qty = {positionQty}", ConsoleColor.Red);
                        interval = 30;
                        goto endLoop;
                    }

                    if (positionQty == 0)
                    {
                        int cancelAllBotOrders()
                        {
                            int canceledOrderCount = 0;
                            int failedOrderCount = 0;
                            foreach (var order in botUpperOrderList)
                            {
                                var canceledResult = apiHelper.CancelActiveOrder(symbol, order.OrderId);
                                if (canceledResult.Result == null)
                                {
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Failed to cancel order:  side = {order.Side}    title = {order.OrderLinkId}    id = {order.OrderId}");
                                    logger.WriteFile("--- " + JObject.FromObject(canceledResult).ToString(Formatting.None));
                                    failedOrderCount++;
                                }
                                else
                                {
                                    canceledOrderCount++;
                                }
                            }
                            foreach (var order in botLowerOrderList)
                            {
                                var canceledResult = apiHelper.CancelActiveOrder(symbol, order.OrderId);
                                if (canceledResult.Result == null)
                                {
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Failed to cancel order:  side = {order.Side}    title = {order.OrderLinkId}    id = {order.OrderId}");
                                    logger.WriteFile("--- " + JObject.FromObject(canceledResult).ToString(Formatting.None));
                                    failedOrderCount++;
                                }
                                else
                                {
                                    canceledOrderCount++;
                                }
                            }
                            if (canceledOrderCount > 0)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} old orders have been canceled. ({failedOrderCount} failed.)");
                                if (failedOrderCount == 0)
                                {
                                    botUpperOrderList = null;
                                    botLowerOrderList = null;
                                }
                            }
                            return failedOrderCount;
                        }
#if LICENSE_MODE
                        if (serverTime.Year != 2022 || serverTime.Month != 2)
                        {
                            cancelAllBotOrders();
                            logger.WriteLine($"This bot is too old. Please contact support.  https://valloontrader.com", ConsoleColor.Green);
                            goto endLoop;
                        }
#endif
                        if (config.Exit == 1)
                        {
                            cancelAllBotOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  No order. (exit = {config.Exit})", ConsoleColor.DarkGray);
                            interval = 30;
                            goto endLoop;
                        }
                        if (lastPrice < param.MinPrice)
                        {
                            cancelAllBotOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Too low price.  now = {lastPrice}. min = {param.MinPrice}", ConsoleColor.DarkGray);
                            interval = 30;
                            goto endLoop;
                        }
                        if (param.MaxPrice > 0 && lastPrice > param.MaxPrice)
                        {
                            cancelAllBotOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Too high price.  now = {lastPrice}. max = {param.MaxPrice}", ConsoleColor.DarkGray);
                            interval = 30;
                            goto endLoop;
                        }

                        if (startPrice == 0 || botUpperOrderList.Count > 0 || botLowerOrderList.Count == 0
                            || lastPositionQty != null && lastPositionQty.Value > 0
                            || 1 - startPrice / lastPrice > param.ResetX
                            || botLowerOrderList.Count == 1 && 1 - botLowerOrderList[0].Price.Value / lastPrice > param.OrderStep)
                        {
                            if (param.OrderQty > availableBalance)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Low available balance.  required = {param.OrderQty}    available = {availableBalance}", ConsoleColor.DarkGray);
                                interval = 30;
                                goto endLoop;
                            }
                            var failedOrcerCount = cancelAllBotOrders();
                            if (failedOrcerCount > 0)
                            {
                                telegramMessage = $"Failed to cancel {failedOrcerCount} orders.";
                                goto endLoop;
                            }
                            {
                                var price = decimal.Round(lastPrice * (1 - param.OrderStep - param.OrderPinned), param.PriceDecimals);
                                decimal qty = decimal.Round(param.OrderQty * position.Leverage.Value * (1 - (0.0006m * 2)) / price, param.QtyDecimals);
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Buy",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = qty,
                                    Price = price,
                                    OrderLinkId = $"<BOT><LIMIT><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Long Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                telegramMessage = $"New Long Order:  price = {price}    qty = {qty}";
                            }
                            iniData["GRID"]["START"] = $"{lastPrice}";
                            iniData["GRID"]["BALANCE"] = $"{walletBalance}";
                            iniDataParser.WriteFile(INI_FILENAME, iniData);
                        }
                    }
                    else if (startPrice == 0)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  StartPrice is 0.  position_qty = {positionQty}", ConsoleColor.DarkGray);
                        interval = 30;
                        goto endLoop;
                    }
                    else
                    {
                        decimal closeQtySum = 0;
                        foreach (var order in activeOrderList)
                        {
                            if (order.Side != "Sell" /* || order.CloseOnTrigger == null || !order.CloseOnTrigger.Value */)
                                continue;
                            closeQtySum += order.Qty.Value;
                        }
                        if (closeQtySum != position.Size.Value)
                        {
                            var filledOrders = apiHelper.GetFullyFilledOrders(symbol, 1);
                            var lastFilledPrice = filledOrders[0].Price.Value;
                            if (filledOrders.Count > 0 && filledOrders[0].Side == "Buy" && Math.Abs(lastFilledPrice - lastPrice) < startPrice * param.OrderStep / 2)
                            {
                                var price = decimal.Round(lastFilledPrice + startPrice * param.OrderStep, param.PriceDecimals);
                                decimal qty = position.Size.Value - closeQtySum;
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Sell",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = qty,
                                    Price = price,
                                    ReduceOnly = true,
                                    OrderLinkId = $"<BOT><CLOSE><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Close Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                telegramMessage = $"New Close Order:  price = {price}    qty = {qty}";
                            }
                        }
                        if (botLowerOrderList.Count == 0 || lastPositionQty > positionQty)
                        {
                            if (param.OrderQty > availableBalance)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Low available balance.  required = {param.OrderQty}    available = {availableBalance}", ConsoleColor.DarkGray);
                            }
                            else
                            {
                                var price = decimal.Round(lastPrice - startPrice * param.OrderStep, param.PriceDecimals);
                                decimal qty = decimal.Round(param.OrderQty * position.Leverage.Value * (1 - (0.0006m * 2)) / price, param.QtyDecimals);
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Buy",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = qty,
                                    Price = price,
                                    OrderLinkId = $"<BOT><LIMIT><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Long Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                telegramMessage = $"New Long Order:  price = {price}    qty = {qty}";
                            }
                        }
                        else if (lastPositionQty == positionQty)
                        {
                            var stopLossPrice = decimal.Round(position.LiqPrice.Value * (1 + param.StopLoss), param.PriceDecimals);
                            if (position.StopLoss == null || position.StopLoss.Value == 0 || Math.Abs(1 - position.StopLoss.Value / stopLossPrice) > param.StopLossReset)
                            {
                                apiHelper.SetPositionStop(symbol: symbol, side: "Buy", stopLoss: stopLossPrice, positionIdx: 0);
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Stop-Loss:  price = {stopLossPrice}");
                            }
                        }
                    }

                endLoop:;
                    lastPositionQty = positionQty;
                    if (telegramMessage != null || loopIndex == 1 || lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                    {
                        string balanceChange = null;
                        if (lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        {
                            decimal value = (walletBalance - lastWalletBalance) / lastWalletBalance * 100;
                            if (value >= 0)
                                balanceChange = $"+{(walletBalance - lastWalletBalance)}   (+{value:N4} %)";
                            else
                                balanceChange = $"{(walletBalance - lastWalletBalance)}   ({value:N4} %)";
                        }
                        string text = $"<pre>{walletBalance:N4}   {lastPrice:F4} / {markPrice:F4}   {activeOrderList.Count} / {(botUpperOrderList == null ? 0 : botUpperOrderList.Count)} / {(botLowerOrderList == null ? 0 : botLowerOrderList.Count)}</pre>";
                        if (balanceChange != null)
                            text += $"\n<pre>{balanceChange}</pre>";
                        if (positionQty != 0)
                        {
                            decimal leverage = 1 / (Math.Abs(positionEntryPrice - position.BustPrice.Value) / positionEntryPrice);
                            decimal unrealisedPnl = position.UnrealisedPnl.Value;
                            decimal unrealisedPercent = 100m * unrealisedPnl / walletBalance;
                            string pnlText;
                            if (unrealisedPnl >= 0)
                                pnlText = $"+{unrealisedPnl}  (+{unrealisedPercent:N2} %)";
                            else
                                pnlText = $"{unrealisedPnl}  ({unrealisedPercent:N2} %)";
                            text += $"\n<pre>Entry = {positionEntryPrice}   Qty = {positionQty}   Liq = {position.LiqPrice}\nLv = {leverage:F2}   SL = {(position.StopLoss == null ? "None" : position.StopLoss.ToString())}   P&L = {pnlText}</pre>";
                        }
                        if (telegramMessage != null)
                            text += $"\n{telegramMessage}";
                        text += $"\n<pre>[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]</pre>";
                        TelegramClient.SendMessageToAdmin(text, Telegram.Bot.Types.Enums.ParseMode.Html);
                        //if (isNewCandle || lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        //    TelegramClient.SendMessageToListenGroup(text, Telegram.Bot.Types.Enums.ParseMode.Html);
                        LastMessage = text;
                    }

                    //usdtBalance = apiHelper.GetWalletBalance("USDT");
                    //walletBalance = usdtBalance._WalletBalance.Value;
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

                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Sleeping {interval} seconds ({apiHelper.RequestCount} requests) ...", ConsoleColor.DarkGray, false);
                    Thread.Sleep(interval * 1000);
                }
                catch (Exception ex)
                {
                    if (logger == null) logger = new Logger($"{BybitLinearApiHelper.ServerTime:yyyy-MM-dd}");
                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]    {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                    logger.WriteFile(ex.ToString());
                    logger.WriteFile($"LastPlain4Sign = {BybitLinearApiHelper.LastPlain4Sign}");
                    TelegramClient.SendMessageToAdmin(ex.ToString());
                    Thread.Sleep(interval * 1000);
                }
                lastLoopTime = DateTime.UtcNow;
            }
        }

    }
}