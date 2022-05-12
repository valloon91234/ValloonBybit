using IO.Swagger.Client;
using IO.Swagger.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;

/**
 * @author Valloon Project
 * @version 1.0 @2020-03-026
 */
namespace Valloon.ByBot
{
    public class RobustStrategy
    {
        public const string BUY = "Buy";
        public const string SELL = "Sell";

        public static int GetStopMarketPrice(PositionInfo position, decimal lastPrice, bool printValue = true)
        {
            int point = (int)(position.EntryPrice.Value + (position.LiqPrice.Value - position.EntryPrice.Value) * GlobalParam.StopLoss);
            decimal nowLoss = 100m * (lastPrice - position.EntryPrice.Value) / (position.LiqPrice.Value - position.EntryPrice.Value);
            if (printValue) Logger.WriteLine($"    stop_loss = {GlobalParam.StopLoss * 100} %    stop_price = {point}    now_loss = {nowLoss:N2} %", ConsoleColor.Yellow);
            return point;
        }

        public static void Run()
        {
            BybitApiHelper apiHelper = new BybitApiHelper(GlobalParam.API_KEY, GlobalParam.API_SECRET, GlobalParam.TESTNET_MODE);
            Logger.WriteLine();
            //apiHelper.GetQueryActiveOrder("efa44157-c355-4a98-b6d6-1d846a936b93");
            int stairs = GlobalParam.Stairs;
            int loopCount = 0;
            bool clearOrder = true;
            bool warningLiquidation = false;
            decimal investUnit;
            {
                decimal investSum = 0;
                for (int i = 0; i < stairs; i++)
                    investSum += GlobalParam.InvestArray[i];
                investUnit = GlobalParam.InvestRatio / investSum;
            }
            while (true)
            {
                try
                {
                    int volume = apiHelper.GetRecentVolume();
                    SymbolTickInfo ticker = apiHelper.GetTicker();
                    decimal lastPrice = decimal.Parse(ticker.LastPrice, CultureInfo.InvariantCulture);
                    decimal markPrice = decimal.Parse(ticker.MarkPrice, CultureInfo.InvariantCulture);
                    //WalletBalance walletBalance = apiHelper.GetWalletBalance();
                    List<OrderRes> activeOrders = apiHelper.GetActiveOrders();
                    int activeOrdersCount = activeOrders.Count;
                    PositionInfo position = apiHelper.GetPosition();
                    int positionQty = (int)position.Size.Value;
                    {
                        string consoleTitle = $"$ {lastPrice:N0}  /  {position.WalletBalance:N8} XBT  /  {activeOrdersCount} Orders";
                        if (positionQty != 0) consoleTitle += "  /  1 Position";
                        consoleTitle += $"    <{GlobalParam.USERNAME}>  |  {GlobalParam.APP_NAME}  v{GlobalParam.APP_VERSION}";
                        Console.Title = consoleTitle;
                        string timeText = DateTime.UtcNow.ToString("yyyy-MM-dd  HH:mm:ss");
                        decimal unavailableMarginPercent = 100m * position.OrderMargin.Value / position.WalletBalance.Value;
                        Logger.WriteLine($"[{timeText}  ({++loopCount})]    $ {lastPrice:N2}  /  $ {markPrice:N2}  /  $ {volume:N0}    {position.WalletBalance:N8} XBT    {activeOrdersCount} orders    {unavailableMarginPercent:N2} %");
                    }
                    decimal markPriceDistance = Math.Abs(markPrice - lastPrice);
                    if (positionQty == 0)
                    {
                        bool resetOrder = (GlobalParam.Direction == 1 || GlobalParam.Direction == 2) && activeOrdersCount != stairs;
                        if (!resetOrder)
                        {
                            if (GlobalParam.Direction == 1)
                            {
                                decimal highest = 0;
                                foreach (OrderRes order in activeOrders)
                                {
                                    if (order.Price != null && order.Price.Value > highest) highest = order.Price.Value;
                                }
                                if (lastPrice - highest > GlobalParam.ResetDistance) resetOrder = true;
                            }
                            else if (GlobalParam.Direction == 2)
                            {
                                decimal lowest = 0;
                                foreach (OrderRes order in activeOrders)
                                {
                                    if (lowest == 0 || order.Price != null && order.Price.Value < lowest) lowest = order.Price.Value;
                                }
                                if (lowest - lastPrice > GlobalParam.ResetDistance) resetOrder = true;
                            }
                        }
                        if (GlobalParam.Direction != 1 && GlobalParam.Direction != 2 && activeOrdersCount != stairs * 2 || resetOrder || clearOrder)
                        {
                            clearOrder = false;
                            if (activeOrdersCount > 0)
                            {
                                apiHelper.CancelAllActiveOrders();
                                Logger.WriteLine($"All of {activeOrdersCount} active orders have been canceled.");
                            }
                            if (volume < GlobalParam.LIMIT_LOWER)
                            {
                                Logger.WriteLine($"No order. (volume < {GlobalParam.LIMIT_LOWER_CANCEL})", ConsoleColor.DarkGray);
                                Thread.Sleep(5000);
                            }
                            else if ((warningLiquidation || GlobalParam.LIMIT_HIGHER_FORCE) && volume > GlobalParam.LIMIT_HIGHER)
                            {
                                Logger.WriteLine($"No order. (volume > {GlobalParam.LIMIT_HIGHER})", ConsoleColor.DarkGray);
                                Thread.Sleep(5000);
                            }
                            else if (markPriceDistance > GlobalParam.LIMIT_MARK)
                            {
                                Logger.WriteLine($"No order. (mark_distance = {markPriceDistance} > {GlobalParam.LIMIT_MARK})", ConsoleColor.DarkGray);
                                Thread.Sleep(5000);
                            }
                            else if (GlobalParam.Exit > 0)
                            {
                                Logger.WriteLine($"No order. (exit = {GlobalParam.Exit})", ConsoleColor.DarkGray);
                                Thread.Sleep(5000);
                            }
                            else
                            {
                                if (volume > GlobalParam.LIMIT_HIGHER_CANCEL)
                                {
                                    Logger.WriteLine($"Warning! Volume is very high. (volume = {volume:N0})", ConsoleColor.DarkYellow);
                                }
                                else if (markPriceDistance > GlobalParam.LIMIT_MARK_CANCEL)
                                {
                                    Logger.WriteLine($"Warning! Mark price is too far. (distance = {markPriceDistance})", ConsoleColor.DarkYellow);
                                }
                                else if (volume < GlobalParam.LIMIT_HIGHER)
                                {
                                    warningLiquidation = false;
                                }
                                int qtyStep = (int)(position.WalletBalance.Value * 1000000 * investUnit);
                                //if (qtyStep < 25)
                                //{
                                //    Logger.WriteLine($"Warning! Wallet balance is too low. (first_order_qty = {qtyStep})", ConsoleColor.DarkYellow);
                                //}
                                if (GlobalParam.Direction == 1)
                                {
                                    for (int i = 0; i < stairs; i++)
                                    {
                                        int qty = (int)(GlobalParam.InvestArray[i] * qtyStep);
                                        int price = GlobalParam.StairsArray[i];
                                        decimal buyPrice = lastPrice - price;
                                        apiHelper.OrderNewLimit(BUY, qty, buyPrice);
                                        Logger.WriteLine($"    <order {i + 1}>\t  qty = {qty}  ({GlobalParam.InvestArray[i]})  \tbuy_price = {buyPrice}  (-{price})");
                                    }
                                    Logger.WriteLine($"{stairs} orders have been created.");
                                }
                                else if (GlobalParam.Direction == 2)
                                {
                                    for (int i = 0; i < stairs; i++)
                                    {
                                        int qty = (int)(GlobalParam.InvestArray[i] * qtyStep);
                                        int price = GlobalParam.StairsArray[i];
                                        decimal sellPrice = lastPrice + price;
                                        apiHelper.OrderNewLimit(SELL, qty, sellPrice);
                                        Logger.WriteLine($"    <order {i + 1}>\t  qty = {qty}  ({GlobalParam.InvestArray[i]})  \tsell_price = {sellPrice}  (+{price})");
                                    }
                                    Logger.WriteLine($"{stairs} orders have been created.");
                                }
                                else
                                {
                                    for (int i = 0; i < stairs; i++)
                                    {
                                        int qty = (int)(GlobalParam.InvestArray[i] * qtyStep);
                                        int price = GlobalParam.StairsArray[i];
                                        decimal sellPrice = lastPrice + price;
                                        decimal buyPrice = lastPrice - price;
                                        apiHelper.OrderNewLimit(BUY, qty, buyPrice);
                                        apiHelper.OrderNewLimit(SELL, qty, sellPrice);
                                        Logger.WriteLine($"    <order {i + 1}>\t  qty = {qty}  ({GlobalParam.InvestArray[i]})  \tbuy_price = {buyPrice}  (-{price})  \tsell_price = {sellPrice}  (+{price})");
                                    }
                                    Logger.WriteLine($"{stairs * 2} orders have been created.");
                                }
                            }
                        }
                        else
                        {
                            if (volume < GlobalParam.LIMIT_LOWER_CANCEL)
                            {
                                apiHelper.CancelAllActiveOrders();
                                Logger.WriteLine($"All of {activeOrdersCount} active orders have been canceled. (volume < {GlobalParam.LIMIT_LOWER_CANCEL})");
                            }
                            else if ((warningLiquidation || GlobalParam.LIMIT_HIGHER_FORCE) && volume > GlobalParam.LIMIT_HIGHER_CANCEL)
                            {
                                apiHelper.CancelAllActiveOrders();
                                Logger.WriteLine($"All of {activeOrdersCount} active orders have been canceled. (volume > {GlobalParam.LIMIT_HIGHER_CANCEL})");
                            }
                            else if (markPriceDistance > GlobalParam.LIMIT_MARK_CANCEL)
                            {
                                apiHelper.CancelAllActiveOrders();
                                Logger.WriteLine($"All of {activeOrdersCount} active orders have been canceled. (mark_distance = {markPriceDistance} > {GlobalParam.LIMIT_MARK_CANCEL})");
                            }
                        }
                    }
                    else
                    {
                        {
                            decimal unrealisedPercent = 100m * position.UnrealisedPnl.Value / position.WalletBalance.Value;
                            Logger.WriteFile($"wallet_balance = {position.WalletBalance}    unrealised_pnl = {position.UnrealisedPnl}    {unrealisedPercent:N2} %    leverage = {position.Leverage}");
                            Logger.WriteLine($"<Position>    qty = {positionQty}    entry = {position.EntryPrice:N2}    liq = {position.LiqPrice}    {unrealisedPercent:N2} %", ConsoleColor.Green);
                        }
                        string side = null;
                        decimal price = 0;
                        decimal profitTarget = GlobalParam.ProfitTarget;
                        bool requiredStopLoss = false;
                        int stopPrice = GetStopMarketPrice(position, lastPrice);
                        if (position.Side == SELL)
                        {
                            side = BUY;
                            price = position.EntryPrice.Value - profitTarget;
                            if (lastPrice > stopPrice) requiredStopLoss = true;
                        }
                        else if (position.Side == BUY)
                        {
                            side = SELL;
                            price = position.EntryPrice.Value + profitTarget;
                            if (lastPrice < stopPrice) requiredStopLoss = true;
                        }
                        if (activeOrdersCount <= 2) warningLiquidation = true;
                        if (GlobalParam.Exit == 2)
                        {
                            apiHelper.CancelAllActiveOrders();
                            OrderRes order = apiHelper.OrderNewMarket(side, positionQty, true);
                            Logger.WriteLine($"Active position has been filled by market. (exit = {GlobalParam.Exit}, price = {lastPrice})", ConsoleColor.DarkYellow);
                            Logger.WriteFile(order);
                            Thread.Sleep(2400);
                            continue;
                        }
                        if (requiredStopLoss)
                        {
                            if (activeOrdersCount <= 1)
                            {
                                apiHelper.CancelAllActiveOrders();
                                OrderRes order = apiHelper.OrderNewMarket(side, positionQty, true);
                                Logger.WriteLine($"Liquidation Alert! Active position has been filled by market. (price = {lastPrice})", ConsoleColor.DarkYellow);
                                Logger.WriteFile(order);
                                Thread.Sleep(2400);
                                continue;
                            }
                            else
                            {
                                apiHelper.CancelAllActiveOrders();
                                Logger.WriteLine($"Warning! Near Liquidation. All orders have been canceled. (price = {lastPrice})", ConsoleColor.DarkYellow);
                            }
                        }
                        bool positionCloseOrderExist = false;
                        for (int i = 0; i < activeOrdersCount; i++)
                        {
                            OrderRes order = activeOrders[i];
                            if (order.Side == side)
                            {
                                if (int.Parse(order.Qty, CultureInfo.InvariantCulture) == positionQty && order.Price != null && Math.Abs(price - order.Price.Value) <= profitTarget)
                                {
                                    positionCloseOrderExist = true;
                                }
                                else
                                {
                                    apiHelper.CancelActiveOrder(order.OrderId);
                                }
                            }
                        }
                        if (!positionCloseOrderExist)
                        {
                            var result = apiHelper.OrderNewLimit(side, positionQty, price, false, true);
                            Logger.WriteLine($"New order for position has been created. (price = {price:N2})", ConsoleColor.Green);
                            Logger.WriteFile(result);
                        }
                        activeOrders = apiHelper.GetActiveOrders();
                        activeOrdersCount = activeOrders.Count;
                        if (activeOrdersCount == 1)
                        {
                            position = apiHelper.GetPosition();
                            if (position.StopLoss.Value != stopPrice)
                            {
                                positionQty = (int)position.Size.Value;
                                stopPrice = GetStopMarketPrice(position, lastPrice, false);
                                var resultStop = apiHelper.SetPositionStopLoss(stopPrice);
                                Logger.WriteLine($"New stop-loss has been created. (stop_price = {stopPrice})", ConsoleColor.Green);
                                Logger.WriteFile(resultStop);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is WebException || ex is ApiException)
                    {
                        Logger.WriteLine("<Server Connection Error>  " + ex.Message, ConsoleColor.Red, false);
                        Logger.WriteFile(ex.ToString());
                    }
                    else if (ex is ApiResultException apiResultException)
                    {
                        Logger.WriteLine($"<API Error in {apiResultException.Name}>", ConsoleColor.Red, true);
                        Logger.WriteLine(apiResultException.ResponseJson, ConsoleColor.DarkGray, true);
                        Logger.WriteFile(ex.ToString());
                    }
                    else
                    {
                        Logger.WriteLine(ex.ToString(), ConsoleColor.Red, true);
                    }
                }
                Thread.Sleep(2000);
            }
        }
    }
}