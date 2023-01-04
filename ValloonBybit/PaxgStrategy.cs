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
            [JsonProperty("qty")]
            public decimal Qty { get; set; }

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

        public void Run()
        {
            int loopIndex = 0;
            DateTime? lastLoopTime = null;
            //decimal? lastPositionQty = null;
            decimal lastWalletBalance = 0;
            string lastParamJson = null;
            GridConfig param = null;
            Logger logger = null;
            DateTime fileModifiedTime = File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location);
            var iniDataParser = new FileIniDataParser();
            while (true)
            {
                var interval = 30;
                string telegramMessage = null;
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
                        logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss fff}  ({++loopIndex})]   $ {lastPrice} / {markPrice:F2}   {walletBalance:N4} / {availableBalance:N4}   {activeOrderList.Count} / {botUpperOrderList.Count} / {botLowerOrderList.Count} / {unavailableMarginPercent:N2} %{balanceChange}", ConsoleColor.White);
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
                    //if (positionQty < 0)
                    //{
                    //    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Short position exists:  Qty = {positionQty}", ConsoleColor.Red);
                    //    interval = 30;
                    //    goto endLoop;
                    //}

                    LinearListOrderResult getLastFilledOrder()
                    {
                        var lastFilledOrders = apiHelper.GetPastOrders(symbol, "New,Filled,PartiallyFilled", 50);
                        if (lastFilledOrders.Count == 0) return null;
                        int count = lastFilledOrders.Count;
                        for (int i = 0; i < count; i++)
                        {
                            var order = lastFilledOrders[i];
                            if (order.OrderStatus == "Filled") continue;
                            if (order.Price == null || Math.Abs(lastPrice - order.Price.Value) > param.PriceStep * 2) continue;
                            var queryResult = apiHelper.GetQueryActiveOrder(symbol, order.OrderId);
                            order.OrderStatus = queryResult.OrderStatus;
                            order.UpdatedTime = queryResult.UpdatedTime;
                        }
                        var sortedOrders = lastFilledOrders.OrderByDescending(o => DateTime.ParseExact(o.UpdatedTime, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture));
                        foreach (var order in sortedOrders)
                        {
                            if (order.OrderStatus == "Filled" || order.OrderStatus == "PartiallyFilled")
                                return order;
                        }
                        return null;
                    }
                    LinearListOrderResult lastFilledOrder = getLastFilledOrder();

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
                            logger.WriteLine($"This bot is too old. Please contact support.  https://t.me/valloon427428", ConsoleColor.Green);
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
                        var closePrice = (lastPrice / param.PriceStep + 1) * param.PriceStep;
                        if (closePrice == lastFilledOrder.Price) closePrice += param.PriceStep;
                        if (closeQtySum < position.Size.Value)
                        {
                            if (closePrice <= lastFilledOrder.Price)
                            {
                            }
                            else if (NoCloseMode)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  <No-Close Mode>", ConsoleColor.DarkGray);
                            }
                            else if (botUpperOrderList.Where(o => o.Price.Value == closePrice).Count() == 0)
                            {
                                var qty = Math.Min(position.Size.Value - closeQtySum, param.Qty);
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
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Close:  price = {closePrice}    qty = {qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                telegramMessage = $"New Close:  price = {closePrice}    qty = {qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price}";
                            }
                            else
                            {
                                var oldOrder = botUpperOrderList.Where(o => o.Price.Value == closePrice).First();
                                var qty = Math.Min(position.Size.Value - closeQtySum + oldOrder.Qty.Value, param.Qty);
                                if (oldOrder.Qty < qty)
                                {
                                    var resultOrder = apiHelper.AmendOrder(new OrderRes
                                    {
                                        OrderId = oldOrder.OrderId,
                                        Symbol = symbol,
                                        Qty = qty,
                                        Price = closePrice,
                                        OrderLinkId = $"<BOT><CLOSE><{BybitLinearApiHelper.ServerTime:yyyyMMdd_HHmmssfff}>",
                                    });
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Amend Close:  price = {closePrice}    qty = {qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price}    oldQty = {oldOrder.Qty}");
                                    logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                    telegramMessage = $"Amend Close:  price = {closePrice}    qty = {qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price}";
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
                        if (limitPrice >= param.MinPrice && (param.MaxPrice == 0 || limitPrice < param.MaxPrice) && limitPrice < lastPrice && botLowerOrderList.Where(o => o.Price.Value == limitPrice).Count() == 0)
                        {
                            var requiredBalance = param.Qty * lastPrice * 2 / position.Leverage;
                            if (requiredBalance > availableBalance)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Low available balance.  required = {requiredBalance}    available = {availableBalance}", ConsoleColor.DarkGray);
                                if (positionQty == 0)
                                    interval = 30;
                            }
                            else
                            {
                                if (positionQty > 0 && lastFilledOrder != null && limitPrice >= lastFilledOrder.Price)
                                {
                                }
                                else if (NoBuyMode)
                                {
                                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  <No-Buy Mode>", ConsoleColor.DarkGray);
                                }
                                else
                                {
                                    var resultOrder = apiHelper.NewOrder(new OrderRes
                                    {
                                        Side = "Buy",
                                        Symbol = symbol,
                                        OrderType = "Limit",
                                        Qty = param.Qty,
                                        Price = limitPrice,
                                        TimeInForce = "PostOnly",
                                        OrderLinkId = $"<BOT><LIMIT><{BybitLinearApiHelper.ServerTime:yyyyMMdd_HHmmssfff}>",
                                        PositionIdx = 0
                                    });
                                    if (lastFilledOrder == null)
                                    {
                                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Long:  price = {limitPrice}    qty = {param.Qty}    last = null");
                                        logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                        telegramMessage = $"New Long:  price = {limitPrice}    qty = {param.Qty}    last = null";
                                    }
                                    else
                                    {
                                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Long:  price = {limitPrice}    qty = {param.Qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price}");
                                        logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                        telegramMessage = $"New Long:  price = {limitPrice}    qty = {param.Qty}    last = {lastFilledOrder.Side} / {lastFilledOrder.OrderStatus} / {lastFilledOrder.Price}";
                                    }
                                }
                            }
                        }
                    }

                endLoop:;
                    //lastPositionQty = positionQty;
                    if (telegramMessage != null || loopIndex == 1 || lastWalletBalance != 0 && lastWalletBalance != walletBalance)
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
                        string text = $"<pre>{walletBalance:N4}   {lastPrice} / {markPrice:F2}   {activeOrderList.Count} / {(botUpperOrderList == null ? 0 : botUpperOrderList.Count)} / {(botLowerOrderList == null ? 0 : botLowerOrderList.Count)}</pre>";
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
                        InlineKeyboardButton.WithCallbackData(text: "Resume Buy & Close", callbackData: $"/resumeBuyAndClose"),
                    },
                });
            if (NoBuyMode)
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData(text: "Resume Buy", callbackData: $"/resumeBuy"),
                    },
                });
            if (NoCloseMode)
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData(text: "Resume", callbackData: $"/resumeClose"),
                    },
                });
            return null;
        }

    }
}