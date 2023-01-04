//#define LICENSE_MODE

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class MMStrategy
    {
        private class MMConfig
        {
            [JsonProperty("symbol")]
            public string Symbol { get; set; }

            [JsonProperty("price_decimals")]
            public int PriceDecimals { get; set; }

            [JsonProperty("qty_decimals")]
            public int QtyDecimals { get; set; }

            [JsonProperty("qty_max_limit")]
            public int QtyMaxLimit { get; set; }

            [JsonProperty("buy_or_sell")]
            public int BuyOrSell { get; set; }

            [JsonProperty("min_price")]
            public decimal MinPrice { get; set; }

            [JsonProperty("max_price")]
            public decimal MaxPrice { get; set; }

            [JsonProperty("upper_order_count")]
            public decimal UpperOrderCount { get; set; }

            [JsonProperty("upper_order_height")]
            public decimal[] UpperOrderHeightArray { get; set; }

            [JsonProperty("upper_order_qty")]
            public decimal[] UpperOrderQtyArray { get; set; }

            [JsonProperty("upper_pinned_x")]
            public decimal UpperPinnedX { get; set; }

            [JsonProperty("upper_close_x")]
            public decimal UpperCloseX { get; set; }

            [JsonProperty("upper_reset_x")]
            public decimal UpperResetX { get; set; }

            [JsonProperty("lower_order_count")]
            public decimal LowerOrderCount { get; set; }

            [JsonProperty("lower_order_height")]
            public decimal[] LowerOrderHeightArray { get; set; }

            [JsonProperty("lower_order_qty")]
            public decimal[] LowerOrderQtyArray { get; set; }

            [JsonProperty("lower_pinned_x")]
            public decimal LowerPinnedX { get; set; }

            [JsonProperty("lower_close_x")]
            public decimal LowerCloseX { get; set; }

            [JsonProperty("lower_reset_x")]
            public decimal LowerResetX { get; set; }

            [JsonProperty("stop_loss")]
            public decimal StopLoss { get; set; }

            [JsonProperty("stop_loss_reset")]
            public decimal StopLossReset { get; set; }

            [JsonProperty("invest_amount")]
            public decimal InvestAmount { get; set; }

            [JsonProperty("interval")]
            public int Interval { get; set; }
        }

        public static string LastMessage;

        public void Run()
        {
            int loopIndex = 0;
            DateTime? lastLoopTime = null;
            decimal lastEntryPrice = 0;
            decimal lastWalletBalance = 0;
            string lastParamJson = null;
            MMConfig param = null;
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
                    string paramJson = File.ReadAllText("config-mm.json");
                    if (param == null || paramJson != lastParamJson)
                    {
                        param = JsonConvert.DeserializeObject<MMConfig>(paramJson);
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
                        logger.WriteLine($"[{BybitLinearApiHelper.ServerTime:yyyy-MM-dd  HH:mm:ss fff}  ({++loopIndex})]   $ {lastPrice:F4} / {markPrice:F4}   {walletBalance:N4}   {activeOrderList.Count} / {botUpperOrderList.Count} / {botLowerOrderList.Count} / {unavailableMarginPercent:N2} %{balanceChange}", ConsoleColor.White);
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
                    if (config.Exit == 2)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  No order. (exit = {config.Exit})", ConsoleColor.DarkGray);
                        goto endLoop;
                    }
                    if (position.PositionIdx != 0 || position.Mode != "MergedSingle" || position.IsIsolated == null || !position.IsIsolated.Value)
                    {
                        logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Invalid Position Mode:  PositionIdx = {position.PositionIdx}    Mode = {position.Mode}    IsIsolated = {position.IsIsolated}", ConsoleColor.Red);
                        goto endLoop;
                    }

                    if (positionQty == 0)
                    {
                        void cancelAllBotOrders()
                        {
                            List<string> cancelOrderList = new List<string>();
                            int canceledOrderCount = 0;
                            foreach (var order in botUpperOrderList)
                            {
                                apiHelper.CancelActiveOrder(symbol, order.OrderId);
                                canceledOrderCount++;
                            }
                            foreach (var order in botLowerOrderList)
                            {
                                apiHelper.CancelActiveOrder(symbol, order.OrderId);
                                canceledOrderCount++;
                            }
                            if (canceledOrderCount > 0)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} old orders have been canceled.");
                                botUpperOrderList = null;
                                botLowerOrderList = null;
                            }
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
                            goto endLoop;
                        }
                        if (param.UpperOrderHeightArray.Length < param.UpperOrderCount || param.UpperOrderQtyArray.Length < param.UpperOrderCount
                            || param.LowerOrderHeightArray.Length < param.LowerOrderCount || param.LowerOrderQtyArray.Length < param.LowerOrderCount)
                        {
                            cancelAllBotOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Invalid config: order count", ConsoleColor.DarkGray);
                            goto endLoop;
                        }
                        if (param.BuyOrSell == 1 && walletBalance < param.InvestAmount)
                        {
                            cancelAllBotOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Low balance.  now = {walletBalance}. min = {param.InvestAmount}", ConsoleColor.DarkGray);
                            goto endLoop;
                        }
                        if (param.BuyOrSell != 1 && walletBalance < param.InvestAmount * 1.1m)
                        {
                            cancelAllBotOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Low balance.  now = {walletBalance}. min = {param.InvestAmount * 1.1m}", ConsoleColor.DarkGray);
                            goto endLoop;
                        }
                        if (lastPrice < param.MinPrice)
                        {
                            cancelAllBotOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Too low price.  now = {lastPrice}. min = {param.MinPrice}", ConsoleColor.DarkGray);
                            goto endLoop;
                        }
                        if (param.MaxPrice > 0 && lastPrice > param.MaxPrice)
                        {
                            cancelAllBotOrders();
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Too high price.  now = {lastPrice}. max = {param.MaxPrice}", ConsoleColor.DarkGray);
                            goto endLoop;
                        }

                        if (param.BuyOrSell == 1 && (lastEntryPrice == 0 || botUpperOrderList.Count > 0 || botLowerOrderList.Count != param.LowerOrderCount || 1 - lastEntryPrice / lastPrice > param.LowerResetX))
                        {
                            cancelAllBotOrders();
                            decimal lowerQtySum = 0;
                            for (int i = 0; i < param.LowerOrderCount; i++)
                            {
                                lowerQtySum += param.LowerOrderQtyArray[i];
                            }
                            decimal lowerQtyX = param.InvestAmount * position.Leverage.Value * (1 - (0.0006m * 2)) / lastPrice / lowerQtySum;
                            for (int i = 0; i < param.LowerOrderCount; i++)
                            {
                                var qty = decimal.Round(lowerQtyX * param.LowerOrderQtyArray[i], param.QtyDecimals);
                                var price = decimal.Round(lastPrice * (1 - param.LowerOrderHeightArray[i] + param.LowerPinnedX), param.PriceDecimals);
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Buy",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = qty,
                                    Price = price,
                                    TimeInForce = "PostOnly",
                                    OrderLinkId = $"<BOT><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Long Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                            }
                            lastEntryPrice = lastPrice;
                        }
                        else if (param.BuyOrSell == 2 && (lastEntryPrice == 0 || botUpperOrderList.Count != param.UpperOrderCount || botLowerOrderList.Count > 0 || lastEntryPrice / lastPrice - 1 > param.LowerResetX))
                        {
                            cancelAllBotOrders();
                            decimal upperQtySum = 0;
                            for (int i = 0; i < param.UpperOrderCount; i++)
                            {
                                upperQtySum += param.UpperOrderQtyArray[i];
                            }
                            decimal upperQtyX = param.InvestAmount * position.Leverage.Value * (1 - (0.0006m * 2)) / lastPrice / upperQtySum;
                            for (int i = 0; i < param.UpperOrderCount; i++)
                            {
                                var qty = decimal.Round(upperQtyX * param.UpperOrderQtyArray[i], param.QtyDecimals);
                                var price = decimal.Round(lastPrice * (1 + param.UpperOrderHeightArray[i] + param.UpperPinnedX), param.PriceDecimals);
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Sell",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = qty,
                                    Price = price,
                                    TimeInForce = "PostOnly",
                                    OrderLinkId = $"<BOT><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Short Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                            }
                            lastEntryPrice = lastPrice;
                        }
                        else if (param.BuyOrSell == 3 && (lastEntryPrice == 0 || botUpperOrderList.Count != param.UpperOrderCount || botLowerOrderList.Count != param.LowerOrderCount))
                        {
                            cancelAllBotOrders();
                            decimal upperQtySum = 0;
                            for (int i = 0; i < param.UpperOrderCount; i++)
                            {
                                upperQtySum += param.UpperOrderQtyArray[i];
                            }
                            decimal upperQtyX = param.InvestAmount * position.Leverage.Value * (1 - (0.0006m * 2)) / lastPrice / upperQtySum;
                            for (int i = 0; i < param.UpperOrderCount; i++)
                            {
                                var qty = decimal.Round(upperQtyX * param.UpperOrderQtyArray[i], param.QtyDecimals);
                                var price = decimal.Round(lastPrice * (1 + param.UpperOrderHeightArray[i] + param.UpperPinnedX), param.PriceDecimals);
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Sell",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = qty,
                                    Price = price,
                                    TimeInForce = "PostOnly",
                                    OrderLinkId = $"<BOT><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Short Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                            }

                            decimal lowerQtySum = 0;
                            for (int i = 0; i < param.LowerOrderCount; i++)
                            {
                                lowerQtySum += param.LowerOrderQtyArray[i];
                            }
                            decimal lowerQtyX = param.InvestAmount * position.Leverage.Value * (1 - (0.0006m * 2)) / lastPrice / lowerQtySum;
                            for (int i = 0; i < param.LowerOrderCount; i++)
                            {
                                var qty = decimal.Round(lowerQtyX * param.LowerOrderQtyArray[i], param.QtyDecimals);
                                var price = decimal.Round(lastPrice * (1 - param.LowerOrderHeightArray[i] + param.LowerPinnedX), param.PriceDecimals);
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Buy",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = qty,
                                    Price = price,
                                    TimeInForce = "PostOnly",
                                    OrderLinkId = $"<BOT><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Long Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                            }
                            lastEntryPrice = lastPrice;
                        }
                    }
                    else if (positionQty > 0)
                    {
                        {
                            List<string> cancelOrderList = new List<string>();
                            int canceledOrderCount = 0;
                            foreach (var order in botUpperOrderList)
                            {
                                if (order.CloseOnTrigger != null && order.CloseOnTrigger.Value)
                                    continue;
                                apiHelper.CancelActiveOrder(symbol, order.OrderId);
                                canceledOrderCount++;
                            }
                            if (canceledOrderCount > 0)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} short orders have been canceled.");
                            }
                        }
                        decimal closeQtySum = 0;
                        foreach (var order in activeOrderList)
                        {
                            if (order.Side != "Sell" /* || order.CloseOnTrigger == null || !order.CloseOnTrigger.Value */)
                                continue;
                            closeQtySum += order.Qty.Value;
                        }
                        if (closeQtySum != position.Size.Value)
                        {
                            int canceledOrderCount = 0;
                            foreach (var order in activeOrderList)
                            {
                                if (order.Side != "Sell" || order.CloseOnTrigger == null || !order.CloseOnTrigger.Value)
                                    continue;
                                apiHelper.CancelActiveOrder(symbol, order.OrderId);
                                canceledOrderCount++;
                            }
                            if (canceledOrderCount > 0)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} close orders have been canceled.");
                            }
                            var qty = position.Size.Value;
                            var price = decimal.Round(positionEntryPrice * (1 + param.LowerCloseX), param.PriceDecimals);
                            while (param.QtyMaxLimit > 0 && qty > param.QtyMaxLimit)
                            {
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Sell",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = param.QtyMaxLimit,
                                    Price = price,
                                    CloseOnTrigger = true,
                                    OrderLinkId = $"<BOT><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Short Close Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                qty -= param.QtyMaxLimit;
                                price += 1m / (int)Math.Pow(10, param.PriceDecimals);
                            }
                            if (qty > 0)
                            {
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Sell",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = qty,
                                    Price = price,
                                    CloseOnTrigger = true,
                                    OrderLinkId = $"<BOT><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Short Close Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                            }
                        }
                        var stopLossPrice = decimal.Round(position.LiqPrice.Value * (1 + param.StopLoss), param.PriceDecimals);
                        if (position.StopLoss == null || position.StopLoss.Value == 0 || Math.Abs(1 - position.StopLoss.Value / stopLossPrice) > param.StopLossReset)
                        {
                            apiHelper.SetPositionStop(symbol: symbol, side: "Buy", stopLoss: stopLossPrice, positionIdx: 0);
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Stop-Loss:  price = {stopLossPrice}");
                        }
                    }
                    else if (positionQty < 0)
                    {
                        {
                            List<string> cancelOrderList = new List<string>();
                            int canceledOrderCount = 0;
                            foreach (var order in botLowerOrderList)
                            {
                                if (order.CloseOnTrigger != null && order.CloseOnTrigger.Value)
                                    continue;
                                apiHelper.CancelActiveOrder(symbol, order.OrderId);
                                canceledOrderCount++;
                            }
                            if (canceledOrderCount > 0)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} long orders have been canceled.");
                            }
                        }
                        decimal closeQtySum = 0;
                        foreach (var order in activeOrderList)
                        {
                            if (order.Side != "Buy" /* || order.CloseOnTrigger == null || !order.CloseOnTrigger.Value */)
                                continue;
                            closeQtySum += order.Qty.Value;
                        }
                        if (closeQtySum != position.Size.Value)
                        {
                            int canceledOrderCount = 0;
                            foreach (var order in activeOrderList)
                            {
                                if (order.Side != "Buy" || order.CloseOnTrigger == null || !order.CloseOnTrigger.Value)
                                    continue;
                                apiHelper.CancelActiveOrder(symbol, order.OrderId);
                                canceledOrderCount++;
                            }
                            if (canceledOrderCount > 0)
                            {
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  {canceledOrderCount} close orders have been canceled.");
                            }
                            var qty = position.Size.Value;
                            var price = decimal.Round(positionEntryPrice * (1 - param.UpperCloseX), param.PriceDecimals);
                            while (param.QtyMaxLimit > 0 && qty > param.QtyMaxLimit)
                            {
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Buy",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = param.QtyMaxLimit,
                                    Price = price,
                                    CloseOnTrigger = true,
                                    OrderLinkId = $"<BOT><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Long Close Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                                qty -= param.QtyMaxLimit;
                                price -= 1m / (int)Math.Pow(10, param.PriceDecimals);
                            }
                            if (qty > 0)
                            {
                                var resultOrder = apiHelper.NewOrder(new OrderRes
                                {
                                    Side = "Buy",
                                    Symbol = symbol,
                                    OrderType = "Limit",
                                    Qty = qty,
                                    Price = price,
                                    CloseOnTrigger = true,
                                    OrderLinkId = $"<BOT><{BybitLinearApiHelper.ServerTime:HHmmssfff}>",
                                    PositionIdx = 0
                                });
                                logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Long Close Order:  price = {price}    qty = {qty}");
                                logger.WriteFile("--- " + JObject.FromObject(resultOrder).ToString(Formatting.None));
                            }
                        }
                        var stopLossPrice = decimal.Round(position.LiqPrice.Value * (1 - param.StopLoss), param.PriceDecimals);
                        if (position.StopLoss == null || position.StopLoss.Value == 0 || Math.Abs(1 - position.StopLoss.Value / stopLossPrice) > param.StopLossReset)
                        {
                            apiHelper.SetPositionStop(symbol: symbol, side: "Sell", stopLoss: stopLossPrice, positionIdx: 0);
                            logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  New Stop-Loss:  price = {stopLossPrice}");
                        }
                    }

                endLoop:;
                    if (loopIndex == 1 || lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                    {
                        string balanceChange = null;
                        if (lastWalletBalance != 0 && lastWalletBalance != walletBalance)
                        {
                            decimal value = (walletBalance - lastWalletBalance) / param.InvestAmount * 100;
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
                            decimal unrealisedPercent = 100m * unrealisedPnl / param.InvestAmount;
                            string pnlText;
                            if (unrealisedPnl >= 0)
                                pnlText = $"+{unrealisedPnl}  (+{unrealisedPercent:N2} %)";
                            else
                                pnlText = $"{unrealisedPnl}  ({unrealisedPercent:N2} %)";
                            text += $"\n<pre>Entry = {positionEntryPrice}   Qty = {positionQty}   Liq = {position.LiqPrice}\nLv = {leverage:F2}   SL = {(position.StopLoss == null ? "None" : position.StopLoss.ToString())}   P&L = {pnlText}</pre>";
                        }
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

                    Thread.Sleep(param.Interval * 1000);
                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]  Sleeping {param.Interval} seconds ({apiHelper.RequestCount} requests) ...", ConsoleColor.DarkGray, false);
                }
                catch (Exception ex)
                {
                    if (logger == null) logger = new Logger($"{BybitLinearApiHelper.ServerTime:yyyy-MM-dd}");
                    logger.WriteLine($"        [{BybitLinearApiHelper.ServerTime:HH:mm:ss fff}]    {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                    logger.WriteFile(ex.ToString());
                    logger.WriteFile($"LastPlain4Sign = {BybitLinearApiHelper.LastPlain4Sign}");
                    TelegramClient.SendMessageToAdmin(ex.ToString());
                    Thread.Sleep(param.Interval * 1000);
                }
                lastLoopTime = DateTime.UtcNow;
            }
        }

    }
}