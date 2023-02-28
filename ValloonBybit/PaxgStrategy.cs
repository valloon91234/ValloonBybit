//#define LICENSE_MODE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using IniParser;
using IO.Swagger.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Types.ReplyMarkups;

/**
 * @author Valloon Present
 * @version 2022-07-18
 * https://www.bybit.com/en-US/help-center/bybitHC_Article?language=en_US&id=000001063
 */
namespace Valloon.Trading
{
    public class PaxgStrategy
    {
        public static readonly string SYMBOL = "PAXGUSDT";

        private class GridConfig
        {
            [JsonProperty("qty_1")]
            public decimal Qty1 { get; set; }

            [JsonProperty("qty_2")]
            public decimal Qty2 { get; set; }

            [JsonProperty("qty_3")]
            public decimal Qty3 { get; set; }

            [JsonProperty("qty_hold_1")]
            public decimal QtyHold1 { get; set; }

            [JsonProperty("qty_hold_2")]
            public decimal QtyHold2 { get; set; }

            [JsonProperty("min_price")]
            public decimal MinPrice { get; set; }

            [JsonProperty("max_price")]
            public decimal MaxPrice { get; set; }

            [JsonProperty("price_step")]
            public int PriceStep { get; set; }

            [JsonProperty("interval")]
            public int Interval { get; set; }
        }

        public static string LastMessage;
        public static bool NoBuyMode;
        public static bool NoCloseMode;
        public static bool NoAmendMode;

        public void Run()
        {
            int loopIndex = 0;
            DateTime? lastLoopTime = null;
            //decimal? lastPositionQty = null;
            decimal lastWalletBalance = 0;
            int lastActiveOrderCount = 0;
            string lastParamJson = null;
            GridConfig param = null;
            Logger logger = null;
            DateTime fileModifiedTime = File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location);
            var iniDataParser = new FileIniDataParser();
            while (true)
            {
                var interval = 30;
                string telegramMessage = "";
                try
                {
                    DateTime currentLoopTime = DateTime.UtcNow;
                    Config config = Config.Load(out bool configUpdated, lastLoopTime != null && lastLoopTime.Value.Day != currentLoopTime.Day);
                    var apiHelper = new BybitLinearApiHelper(config.ApiKey, config.ApiSecret, config.TestnetMode);
                    logger = new Logger($"{BybitLinearApiHelper.ServerTime:yyyy-MM-dd}");
                    if (lastLoopTime == null)
                        logger.WriteLine($"\r\n[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]  Grid bot started.", ConsoleColor.Green);
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
                    string paramJson = File.ReadAllText("config-paxg.json");
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
                    var symbol = SYMBOL;
                    var ticker = apiHelper.GetTicker(symbol);
                    int lastPrice = (int)ticker.LastPrice.Value;
                    decimal markPrice = ticker.MarkPrice.Value;
                    var activeOrderList = apiHelper.GetActiveOrders(symbol);
                    var upperOrderList = new List<LinearListOrderResult>();
                    var lowerOrderList = new List<LinearListOrderResult>();
                    foreach (var order in activeOrderList)
                    {
                        //if (!order.OrderLinkId.Contains("<BOT>")) continue;
                        if (order.Side == "Sell")
                            upperOrderList.Add(order);
                        else if (order.Side == "Buy")
                            lowerOrderList.Add(order);
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
                        logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss fff}  ({++loopIndex})]   $ {lastPrice} / {markPrice:F2}   {walletBalance:N4} / {availableBalance:N4}   {activeOrderList.Count} / {upperOrderList.Count} / {lowerOrderList.Count} / {unavailableMarginPercent:N2} %{balanceChange}", ConsoleColor.White);
                    }
                    decimal positionEntryPrice = 0;
                    decimal positionQty = position.Side == "Buy" ? position.Size.Value : -position.Size.Value;
                    {
                        string consoleTitle = $"$ {lastPrice}  /  {walletBalance}";
                        if (positionQty != 0) consoleTitle += "  /  1 Position";
                        consoleTitle += $"    <{config.Username}>  |   {Config.APP_NAME}  v{fileModifiedTime:yyyy.MM.dd HH:mm:ss}   by  @valloon427428   ( https://t.me/valloon427428 )";
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
                                pnlText = $"+{unrealisedPnl:N4} (+{unrealisedPercent:N2} %)";
                            else
                                pnlText = $"{unrealisedPnl:N4} ({unrealisedPercent:N2} %)";
                            logger.WriteLine($"    Entry = {positionEntryPrice}   qty = {positionQty}   liq = {position.LiqPrice}   lv = {leverage:F2}   TP = {(position.TakeProfit == null ? "None" : position.TakeProfit.ToString())}   SL = {(position.StopLoss == null ? "None" : position.StopLoss.ToString())}   P&L = {pnlText}", ConsoleColor.Green);
                        }
                    }
                    LinearListOrderResult lastFilledOrder = null;
                    string lastFilledOrderMessage = null;
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
                    if (param.Qty1 <= 0 || param.Qty2 <= 0)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Invalid Qty:  Qty1 = {param.Qty1}    Qty2 = {param.Qty2}", ConsoleColor.Red);
                        interval = 30;
                        goto endLoop;
                    }
                    if (param.PriceStep <= 0)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Invalid PriceStep:  {param.PriceStep}", ConsoleColor.Red);
                        interval = 30;
                        goto endLoop;
                    }
                    if (lastActiveOrderCount != 0 && activeOrderList.Count == 0)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  ActiveOrderCount = {activeOrderList.Count}, last = {lastActiveOrderCount}", ConsoleColor.Red);
                        interval = 15;
                        goto endLoop;
                    }

                    LinearListOrderResult getLastFilledOrder(out string message)
                    {
                        var lastFilledOrders = apiHelper.GetLastOrders(symbol, "New,PartiallyFilled", 50);
                        int count = lastFilledOrders.Count;
                        if (activeOrderList.Count > 0 && count == 0)
                        {
                            message = $"No New or PartiallyFilled";
                            return null;
                        }
                        for (int i = 0; i < count; i++)
                        {
                            var order = lastFilledOrders[i];
                            if (order.Price == null || Math.Abs(lastPrice - order.Price.Value) > param.PriceStep * 2) continue;
                            var queryResult = apiHelper.GetQueryActiveOrder(symbol, order.OrderId);
                            order.OrderStatus = queryResult.OrderStatus;
                            order.UpdatedTime = queryResult.UpdatedTime;
                        }
                        var lastFilledOrders2 = apiHelper.GetLastOrders(symbol, "PartiallyFilled,Filled", 50);
                        if (positionQty != 0 && lastFilledOrders2.Count == 0)
                        {
                            message = $"No Filled or PartiallyFilled";
                            return null;
                        }
                        foreach (var order in lastFilledOrders2)
                        {
                            //if (order.OrderStatus == "PartiallyFilled") continue;
                            lastFilledOrders.Add(order);
                        }
                        var sortedOrders = lastFilledOrders.OrderByDescending(o => DateTime.ParseExact(o.UpdatedTime, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture));
                        foreach (var order in sortedOrders)
                        {
                            if (order.OrderStatus == "Filled" || order.OrderStatus == "PartiallyFilled")
                            {
                                message = null;
                                return order;
                            }
                        }
                        message = $"No Filled or PartiallyFilled in {lastFilledOrders.Count} orders";
                        return null;
                    }
                    lastFilledOrder = getLastFilledOrder(out lastFilledOrderMessage);

                    if (positionQty == 0)
                    {
                        void cancelAllOrders()
                        {
                            apiHelper.CancelAllActiveOrders(symbol);
                        }
#if LICENSE_MODE
                        if (serverTime.Year != 2022 || serverTime.Month != 2)
                        {
                            cancelAllBotOrders();
                            logger.WriteLine($"This bot is too old. Please contact support.  https://t.me/valloon427428", ConsoleColor.Green);
                            goto endLoop;
                        }
#endif
                        if (config.Exit == 1)
                        {
                            cancelAllOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  No order. (exit = {config.Exit})", ConsoleColor.DarkGray);
                            interval = 30;
                            goto endLoop;
                        }
                        if (lastPrice < param.MinPrice)
                        {
                            cancelAllOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Too low price.  now = {lastPrice}. min = {param.MinPrice}", ConsoleColor.DarkGray);
                            interval = 30;
                            goto endLoop;
                        }
                        if (param.MaxPrice > 0 && lastPrice > param.MaxPrice)
                        {
                            cancelAllOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Too high price.  now = {lastPrice}. max = {param.MaxPrice}", ConsoleColor.DarkGray);
                            interval = 30;
                            goto endLoop;
                        }
                    }
                    else if (positionQty > 0)
                    {
                        //if (lastPositionQty == positionQty)
                        //{
                        decimal closeQtySum = 0;
                        foreach (var order in activeOrderList)
                        {
                            if (order.Side != "Sell" /* || order.CloseOnTrigger == null || !order.CloseOnTrigger.Value */)
                                continue;
                            closeQtySum += order.Qty.Value;
                        }
                        if (lastFilledOrder == null)
                        {
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]    lastFilledOrder is Null: {lastFilledOrderMessage}", ConsoleColor.Red);
                            telegramMessage += $"lastFilledOrder is Null: {lastFilledOrderMessage}\n";
                        }
                        else if (closeQtySum < position.Size.Value)
                        {
                            var closePrice = (lastPrice / param.PriceStep + 1) * param.PriceStep;
                            if (closePrice == lastFilledOrder.Price) closePrice += param.PriceStep;
                            if (lastFilledOrder.Side == "Buy" && lastFilledOrder.OrderStatus == "PartiallyFilled" && closePrice - lastFilledOrder.Price <= param.PriceStep) closePrice += param.PriceStep;
                            if (closePrice <= lastFilledOrder.Price)
                            {
                            }
                            else if (NoCloseMode)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  <No-Close Mode>", ConsoleColor.DarkGray);
                            }
                            else if (upperOrderList.Where(o => o.Price.Value == closePrice).Count() == 0)
                            {
                                var qty = position.Size.Value - closeQtySum;
                                if (positionQty > param.QtyHold1)
                                    qty = Math.Min(qty, param.Qty1);
                                else
                                    qty = Math.Min(qty, param.Qty2);
                                if (lastFilledOrder.Side == "Buy" && lastFilledOrder.OrderStatus == "Filled")
                                    qty = Math.Min(qty, lastFilledOrder.CumExecQty.Value);
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Sell",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = qty,
                                    Price = closePrice,
                                    TimeInForce = "PostOnly",
                                    ReduceOnly = true,
                                    OrderLinkId = $"<BOT><CLOSE><{BybitLinearApiHelper.ServerTime:yyyyMMdd_HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Close:  price = {closePrice}    qty = {qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price} / {lastFilledOrder.Qty} / {lastFilledOrder.CumExecQty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                telegramMessage += $"New Close:  price = {closePrice}    qty = {qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price} / {lastFilledOrder.Qty} / {lastFilledOrder.CumExecQty}\n";
                            }
                            else
                            {
                                var oldOrder = upperOrderList.Where(o => o.Price.Value == closePrice).First();
                                var qty = position.Size.Value - closeQtySum + oldOrder.Qty.Value;
                                if (positionQty > param.QtyHold1)
                                    qty = Math.Min(qty, param.Qty1);
                                else
                                    qty = Math.Min(qty, param.Qty2);
                                if (lastFilledOrder.Side == "Buy" && lastFilledOrder.OrderStatus == "Filled")
                                    qty = Math.Min(qty, lastFilledOrder.CumExecQty.Value);
                                if (oldOrder.Qty < qty)
                                {
                                    if (NoAmendMode)
                                    {
                                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  <No-Amend Mode>", ConsoleColor.DarkGray);
                                    }
                                    else
                                    {
                                        var resultOrder = apiHelper.AmendOrder(new OrderRes
                                        {
                                            OrderId = oldOrder.OrderId,
                                            Symbol = symbol,
                                            Qty = qty,
                                            Price = closePrice,
                                            OrderLinkId = $"<BOT><CLOSE><{BybitLinearApiHelper.ServerTime:yyyyMMdd_HHmmssfff}>",
                                        });
                                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Amend Close:  price = {closePrice}    qty = {oldOrder.Qty} -> {qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price} / {lastFilledOrder.Qty} / {lastFilledOrder.CumExecQty}");
                                        logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                        telegramMessage += $"Amend Close:  price = {closePrice}    qty = {oldOrder.Qty} -> {qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price} / {lastFilledOrder.Qty} / {lastFilledOrder.CumExecQty}\n";
                                    }
                                }
                            }
                        }

                        var stopLossPrice = Math.Ceiling(position.LiqPrice.Value) + 1;
                        if (position.StopLoss == null || position.StopLoss.Value != stopLossPrice)
                        {
                            apiHelper.SetPositionStop(symbol: symbol, side: "Buy", stopLoss: stopLossPrice, positionIdx: 0);
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Stop-Loss:  price = {stopLossPrice}");
                        }
                        //}
                    }
                    else if (positionQty < 0)
                    {

                    }

                    //if (lastPositionQty == positionQty)
                    {
                        var limitPrice = lastPrice / param.PriceStep * param.PriceStep;
                        if (positionQty == 0 && limitPrice >= lastPrice || positionQty > 0 && lastFilledOrder != null && limitPrice == lastFilledOrder.Price) limitPrice -= param.PriceStep;
                        if (positionQty > 0 && lastFilledOrder.Side == "Sell" && lastFilledOrder.OrderStatus == "PartiallyFilled" && lastFilledOrder.Price - limitPrice <= param.PriceStep) limitPrice -= param.PriceStep;
                        if (limitPrice >= param.MinPrice && (param.MaxPrice == 0 || limitPrice < param.MaxPrice) && limitPrice < lastPrice && lowerOrderList.Where(o => o.Price.Value == limitPrice).Count() == 0)
                        {
                            var qty = param.Qty1;
                            var requiredBalance = qty * lastPrice * 2 / position.Leverage;
                            if (requiredBalance > availableBalance)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Low available balance.  required = {requiredBalance}    available = {availableBalance}", ConsoleColor.DarkGray);
                                if (positionQty == 0)
                                    interval = 30;
                            }
                            else
                            {
                                if (positionQty > 0 && lastFilledOrder == null)
                                {
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]    lastFilledOrder is Null: {lastFilledOrderMessage}", ConsoleColor.Red);
                                    telegramMessage += $"lastFilledOrder is Null: {lastFilledOrderMessage}\n";
                                }
                                else if (positionQty > 0 && lastFilledOrder != null && limitPrice >= lastFilledOrder.Price)
                                {
                                }
                                else if (NoBuyMode)
                                {
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  <No-Buy Mode>", ConsoleColor.DarkGray);
                                }
                                else
                                {
                                    if (positionQty > param.QtyHold2 && positionEntryPrice <= limitPrice)
                                        qty = param.Qty3;
                                    var resultOrder = apiHelper.NewOrder(new OrderRes
                                    {
                                        Side = "Buy",
                                        Symbol = symbol,
                                        OrderType = "Limit",
                                        Qty = qty,
                                        Price = limitPrice,
                                        TimeInForce = "PostOnly",
                                        OrderLinkId = $"<BOT><LIMIT><{BybitLinearApiHelper.ServerTime:yyyyMMdd_HHmmssfff}>",
                                        PositionIdx = 0
                                    });
                                    if (lastFilledOrder == null)
                                    {
                                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Long:  price = {limitPrice}    qty = {qty}    last = Null / {lastFilledOrderMessage}");
                                        logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                        telegramMessage += $"New Long:  price = {limitPrice}    qty = {qty}    last = Null / {lastFilledOrderMessage}\n";
                                    }
                                    else
                                    {
                                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Long:  price = {limitPrice}    qty = {qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price} / {lastFilledOrder.Qty} / {lastFilledOrder.CumExecQty}");
                                        logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                        telegramMessage += $"New Long:  price = {limitPrice}    qty = {qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price} / {lastFilledOrder.Qty} / {lastFilledOrder.CumExecQty}\n";
                                    }
                                }
                            }
                        }
                    }

                endLoop:;
                    //lastPositionQty = positionQty;
                    {
                        string balanceChange = null;
                        if (lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        {
                            decimal value = (walletBalance - lastWalletBalance) / lastWalletBalance * 100;
                            if (value >= 0)
                                balanceChange = $"🔥  +{(walletBalance - lastWalletBalance)}   (+{value:N4} %)";
                            else
                                balanceChange = $"{(walletBalance - lastWalletBalance)}   ({value:N4} %)";
                        }
                        string text = $"<pre>{walletBalance:N4}   {lastPrice} / {markPrice:F2}   #{loopIndex} / {activeOrderList.Count} / {(upperOrderList == null ? 0 : upperOrderList.Count)} / {(lowerOrderList == null ? 0 : lowerOrderList.Count)}</pre>";
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
                        if (!string.IsNullOrWhiteSpace(telegramMessage))
                            text += $"\n{telegramMessage.Trim()}";
                        else if (lastFilledOrder == null)
                            text += $"\nlast = Null / {lastFilledOrderMessage}";
                        else
                            text += $"\nlast = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price} / {lastFilledOrder.Qty} / {lastFilledOrder.CumExecQty}";

                        text += $"\n<pre>[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}]</pre>";
                        if (!string.IsNullOrWhiteSpace(telegramMessage) || loopIndex == 1 || lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                            TelegramClient.SendMessageToAdmin(text, Telegram.Bot.Types.Enums.ParseMode.Html, GetReplyMarkup());
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
                            logger2.WriteFile($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss}  ({++loopIndex:D6})]    {walletBalance:F8}    {walletBalance - lastWalletBalance:F8}    price = {lastPrice}  /  {markPrice:F2}        balance = {(walletBalance - lastWalletBalance) / lastWalletBalance * 100:N4}%{suffix}");
                        }
                        lastWalletBalance = walletBalance;
                    }
                    lastActiveOrderCount = activeOrderList.Count;

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

        public static InlineKeyboardMarkup GetReplyMarkup()
        {
            if (NoBuyMode && NoCloseMode)
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData(text: "Resume Buy & Close", callbackData: $"/resume_buy_and_close"),
                    },
                });
            if (NoBuyMode)
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData(text: "Resume Buy", callbackData: $"/resume_buy"),
                    },
                });
            if (NoCloseMode)
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData(text: "Resume Close", callbackData: $"/resume_close"),
                    },
                });
            if (NoAmendMode)
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData(text: "Resume Amend", callbackData: $"/resume_amend"),
                    },
                });
            return null;
        }

    }
}