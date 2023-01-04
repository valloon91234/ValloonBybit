using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

/**
 * @author Valloon Project
 * @version 1.0 @2020-03-25
 * @version 2.0 @2022-05-09
 */
namespace Valloon.Trading
{
    public class BybitLinearApiHelper
    {
        public static int FixQty(int qty, int digits = 2)
        {
            //double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs((double)qty))) + 1);
            //return scale * Math.Round(((double)qty) / scale, digits);
            double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(qty))) + 1 - digits);
            return (int)(scale * Math.Truncate(qty / scale));
        }

        public static int GetX(string symbol)
        {
            switch (symbol)
            {
                case SYMBOL_BTCUSDT:
                    return 100;
                case SYMBOL_ETHUSDT:
                    return 100;
                case SYMBOL_BNBUSDT:
                    return 100;
                case SYMBOL_SOLUSDT:
                    return 1000;
                case SYMBOL_AVAXUSDT:
                    return 1000;
                case SYMBOL_ADAUSDT:
                    return 10000;
                case SYMBOL_ALGOUSDT:
                    return 10000;
                case SYMBOL_MATICUSDT:
                    return 10000;
                case SYMBOL_TRXUSDT:
                    return 100000;
                case SYMBOL_NEARUSDT:
                    return 1000;
                case SYMBOL_FTMUSDT:
                    return 10000;
                case SYMBOL_DOGEUSDT:
                    return 10000;
                case SYMBOL_SANDUSDT:
                    return 10000;
                case SYMBOL_1000LUNCUSDT:
                    return 10000;
                default:
                    throw new ArgumentException($"Invalid symbol: {symbol}");
            }
        }

        //private static long GetExpires()
        //{
        //    return (long)(DateTime.UtcNow - Jan1st1970).TotalSeconds + 3600; // set expires one hour in the future
        //}

        private static string ByteArrayToString(byte[] ba)
        {
            var hex = new StringBuilder(ba.Length * 2);
            foreach (var b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }

        private static byte[] Hmacsha256(byte[] keyByte, byte[] messageBytes)
        {
            using (var hash = new HMACSHA256(keyByte))
            {
                return hash.ComputeHash(messageBytes);
            }
        }

        public const string SYMBOL_BTCUSDT = "BTCUSDT";
        public const string SYMBOL_ETHUSDT = "ETHUSDT";
        public const string SYMBOL_BNBUSDT = "BNBUSDT";
        public const string SYMBOL_SOLUSD = "SOLUSD";
        public const string SYMBOL_SOLUSDT = "SOLUSDT";
        public const string SYMBOL_AVAXUSDT = "AVAXUSDT";

        public const string SYMBOL_ADAUSDT = "ADAUSDT";
        public const string SYMBOL_ALGOUSDT = "ALGOUSDT";
        public const string SYMBOL_MATICUSDT = "MATICUSDT";
        public const string SYMBOL_TRXUSDT = "TRXUSDT";
        public const string SYMBOL_NEARUSDT = "NEARUSDT";
        public const string SYMBOL_FTMUSDT = "FTMUSDT";
        public const string SYMBOL_DOGEUSDT = "DOGEUSDT";
        public const string SYMBOL_SANDUSDT = "SANDUSDT";
        public const string SYMBOL_1000LUNCUSDT = "1000LUNCUSDT";
        public const string TIME_IN_FORCE_GTC = "GoodTillCancel";


        public readonly string API_KEY;
        public readonly string API_SECRET;

        private readonly MarketApi MarketApiInstance;
        private readonly LinearKlineApi KlineApiInstance;
        private readonly LinearOrderApi OrderApiInstance;
        private readonly LinearConditionalApi ConditionalApiInstance;
        private readonly LinearPositionsApi PositionsApiInstance;
        private readonly WalletApi WalletApiInstance;

        private static long _serverTimeDiff;
        public static DateTime ServerTime
        {
            get
            {
                return DateTime.UtcNow.AddTicks(_serverTimeDiff);
            }
            set
            {
                _serverTimeDiff = (value - DateTime.UtcNow).Ticks;
            }
        }

        public int RequestCount { get; set; }
        public static string LastPlain4Sign { get; set; }

        public BybitLinearApiHelper(string apiKey = null, string apiSecret = null, bool testnet = false)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            API_KEY = apiKey;
            API_SECRET = apiSecret;
            {
                CommonApi api = new CommonApi();
                JObject jObject = (JObject)api.CommonGetTime();
                ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            }
            if (!testnet) Configuration.Default.BasePath = "https://api.bybit.com";
            MarketApiInstance = new MarketApi();
            KlineApiInstance = new LinearKlineApi();
            OrderApiInstance = new LinearOrderApi();
            ConditionalApiInstance = new LinearConditionalApi();
            PositionsApiInstance = new LinearPositionsApi();
            WalletApiInstance = new WalletApi();
        }

        private string CreateSignature(List<KeyValuePair<string, string>> paramList)
        {
            string timestampString = (ServerTime.ToJavaMilliseconds()).ToString();
            paramList.Add(new KeyValuePair<string, string>("api_key", API_KEY));
            paramList.Add(new KeyValuePair<string, string>("timestamp", timestampString));
            string message = "";
            var paramListOrdered = paramList.OrderBy(n => n.Key);
            foreach (var param in paramListOrdered)
            {
                if (param.Value != null && param.Value != "")
                    message += $"&{param.Key}={param.Value}";
            }
            message = message.Substring(1);
            var signatureBytes = Hmacsha256(Encoding.UTF8.GetBytes(API_SECRET), Encoding.UTF8.GetBytes(message));
            string sign = ByteArrayToString(signatureBytes);
            Configuration.Default.ApiKey["api_key"] = API_KEY;
            Configuration.Default.ApiKey["timestamp"] = timestampString;
            Configuration.Default.ApiKey["sign"] = sign;
            LastPlain4Sign = message;
            return message;
        }

        public List<KlineRes> GetCandleList(string symbol, string interval, DateTime? startTime = null, int? limit = null)
        {
            RequestCount++;
            long? from = null;
            if (startTime != null) from = startTime.Value.ToJavaMilliseconds() / 1000;
            JObject jObject = (JObject)KlineApiInstance.LinearKlineGet(symbol, interval, from, limit);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var result = jObject.ToObject<KlineBase>().Result;
            if (result == null) throw new ApiResultException("GetCandleList", jObject);
            return result;
        }

        public SymbolTickInfo GetTicker(string symbol)
        {
            RequestCount++;
            JObject jObject = (JObject)MarketApiInstance.MarketSymbolInfo(symbol);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<SymbolInfoBase>();
            List<SymbolTickInfo> resultList = obj.Result;
            if (resultList.Count > 0) return resultList[0];
            return null;
        }

        public List<LinearListOrderResult> GetPastOrders(string symbol, string orderStatus, int limit = 50)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", symbol),
                new KeyValuePair<string, string>("limit", limit.ToString()),
                new KeyValuePair<string, string>("order_status", orderStatus)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.LinearOrderGetOrders(null, null, symbol, null, null, limit, orderStatus);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<LinearOrderRecordsResponseBase>();
            if (obj.Result == null) throw new ApiResultException("GetActiveOrders", jObject);
            var data = ((JObject)obj.Result).ToObject<LinearOrderRecordsResponse>();
            return data.Data ?? new List<LinearListOrderResult>();
        }


        public List<LinearListOrderResult> GetFullyFilledOrders(string symbol, int limit = 50)
        {
            return GetPastOrders(symbol, "Filled", limit);
        }

        public List<LinearListOrderResult> GetFilledOrders(string symbol, int limit = 50)
        {
            return GetPastOrders(symbol, "Filled,PartiallyFilled", limit);
        }

        public List<LinearListOrderResult> GetActiveOrders(string symbol, int limit = 50)
        {
            return GetPastOrders(symbol, "New,PartiallyFilled", limit);
        }

        public QueryOrderRes GetQueryActiveOrder(string symbol, string orderId, string orderLinkId = null)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("order_id", orderId),
                new KeyValuePair<string, string>("order_link_id", orderLinkId),
                new KeyValuePair<string, string>("symbol", symbol)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.LinearOrderQuery(symbol, orderId, orderLinkId);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<QueryOrderBase>();
            if (obj.Result == null) throw new ApiResultException("GetQueryActiveOrder", jObject);
            var result = ((JObject)obj.Result).ToObject<QueryOrderRes>();
            return result;
        }

        public OrderCancelBase CancelActiveOrder(string symbol, string orderId, string orderLinkId = null)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", symbol),
                new KeyValuePair<string, string>("order_id", orderId)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.LinearOrderCancel(orderId, orderLinkId, symbol);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            return jObject.ToObject<OrderCancelBase>();
            //var obj = jObject.ToObject<OrderCancelBase>();
            //if (obj.Result == null) return null;
            //var data = ((JObject)obj.Result).ToObject<OrderRes>();
            //return data;
        }

        public List<OrderCancelAllRes> CancelAllActiveOrders(string symbol)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", symbol)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.LinearOrderCancelAll(symbol);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<OrderCancelAllBase>();
            if (obj.Result == null) return null;
            var data = ((JArray)obj.Result).ToObject<List<OrderCancelAllRes>>();
            return data;
        }

        public OrderRes NewOrder(OrderRes order)
        {
            RequestCount++;
            if (order.TimeInForce == null) order.TimeInForce = TIME_IN_FORCE_GTC;
            if (order.ReduceOnly == null) order.ReduceOnly = false;
            if (order.CloseOnTrigger == null) order.CloseOnTrigger = false;
            if (order.PositionIdx == null) order.PositionIdx = 0;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("side", order.Side),
                new KeyValuePair<string, string>("symbol", order.Symbol),
                new KeyValuePair<string, string>("order_type", order.OrderType),
                new KeyValuePair<string, string>("qty", order.Qty?.ToString()),
                new KeyValuePair<string, string>("price", order.Price?.ToString()),
                new KeyValuePair<string, string>("time_in_force", order.TimeInForce ),
                new KeyValuePair<string, string>("reduce_only", order.ReduceOnly?.ToString()),
                new KeyValuePair<string, string>("close_on_trigger",order.CloseOnTrigger?.ToString()),
                new KeyValuePair<string, string>("order_link_id",order.OrderLinkId),
                new KeyValuePair<string, string>("take_profit",order.TakeProfit?.ToString()),
                new KeyValuePair<string, string>("stop_loss",order.StopLoss?.ToString()),
                new KeyValuePair<string, string>("tp_trigger_by",order.TpTriggerBy),
                new KeyValuePair<string, string>("sl_trigger_by",order.SlTriggerBy),
                new KeyValuePair<string, string>("position_idx",order.PositionIdx.ToString()),
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.LinearOrderNew(order.Symbol, order.Side, order.OrderType, order.TimeInForce, order.Qty, order.Price, order.TakeProfit, order.StopLoss, order.ReduceOnly, order.TpTriggerBy, order.SlTriggerBy, order.CloseOnTrigger, order.OrderLinkId, order.PositionIdx);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<OrderResBase>();
            if (obj.Result == null) throw new ApiResultException("OrderNew", jObject);
            var data = ((JObject)obj.Result).ToObject<OrderRes>();
            return data;
        }

        public OrderRes AmendOrder(OrderRes order)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("order_id", order.OrderId),
                new KeyValuePair<string, string>("order_link_id", order.OrderLinkId),
                new KeyValuePair<string, string>("symbol", order.Symbol),
                new KeyValuePair<string, string>("p_r_qty", order.Qty?.ToString()),
                new KeyValuePair<string, string>("p_r_price", order.Price?.ToString()),
                new KeyValuePair<string, string>("take_profit", order.TakeProfit?.ToString()),
                new KeyValuePair<string, string>("stop_loss", order.StopLoss?.ToString()),
                new KeyValuePair<string, string>("tp_trigger_by", order.TpTriggerBy),
                new KeyValuePair<string, string>("sl_trigger_by", order.SlTriggerBy),
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.LinearOrderReplace(order.Symbol, order.OrderId, order.OrderLinkId, order.Qty, order.Price, order.TakeProfit, order.StopLoss, order.TpTriggerBy, order.SlTriggerBy);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<OrderResBase>();
            if (obj.Result == null) throw new ApiResultException("OrderAmendStop", jObject);
            var data = ((JObject)obj.Result).ToObject<OrderRes>();
            return data;
        }

        public List<LinearListStopOrderResult> GetActiveStopOrders(string symbol)
        {
            RequestCount++;
            int limit = 50;
            string orderStatus = "New,PartiallyFilled";
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", symbol),
                new KeyValuePair<string, string>("limit", limit.ToString()),
                new KeyValuePair<string, string>("stop_order_status", orderStatus)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)ConditionalApiInstance.LinearConditionalGetOrders(null, null, symbol, null, null, limit, orderStatus);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<LinearStopOrderRecordsResponseBase>();
            if (obj.Result == null) throw new ApiResultException("GetActiveStopOrders", jObject);
            var data = ((JObject)obj.Result).ToObject<LinearStopOrderRecordsResponse>();
            return data.Data ?? new List<LinearListStopOrderResult>();
        }

        public QueryOrderRes GetQueryActiveStopOrder(string symbol, string orderId, string orderLinkId = null)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("order_id", orderId),
                new KeyValuePair<string, string>("symbol", symbol)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)ConditionalApiInstance.LinearConditionalQuery(symbol, orderId, orderLinkId);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<QueryOrderBase>();
            if (obj.Result == null) throw new ApiResultException("GetQueryActiveOrder", jObject);
            var result = ((JObject)obj.Result).ToObject<QueryOrderRes>();
            return result;
        }

        public OrderRes CancelActiveStopOrder(string symbol, string stopOrderId, string orderLinkId = null)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", symbol),
                new KeyValuePair<string, string>("stop_order_id", stopOrderId)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)ConditionalApiInstance.LinearConditionalCancel(stopOrderId, orderLinkId, symbol);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<OrderCancelBase>();
            if (obj.Result == null) return null;
            var data = ((JObject)obj.Result).ToObject<OrderRes>();
            return data;
        }

        public List<OrderCancelAllRes> CancelAllActiveStopOrders(string symbol)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", symbol)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)ConditionalApiInstance.LinearConditionalCancelAll(symbol);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<OrderCancelAllBase>();
            if (obj.Result == null) return null;
            var data = ((JArray)obj.Result).ToObject<List<OrderCancelAllRes>>();
            return data;
        }

        public ConditionalRes NewStopOrder(ConditionalRes order)
        {
            RequestCount++;
            if (order.TimeInForce == null) order.TimeInForce = TIME_IN_FORCE_GTC;
            if (order.ReduceOnly == null) order.ReduceOnly = false;
            if (order.CloseOnTrigger == null) order.CloseOnTrigger = false;
            if (order.PositionIdx == null) order.PositionIdx = 0;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("side", order.Side),
                new KeyValuePair<string, string>("symbol", order.Symbol),
                new KeyValuePair<string, string>("order_type", order.OrderType),
                new KeyValuePair<string, string>("qty", order.Qty?.ToString()),
                new KeyValuePair<string, string>("price", order.Price?.ToString()),
                new KeyValuePair<string, string>("base_price", order.BasePrice?.ToString()),
                new KeyValuePair<string, string>("stop_px", order.StopPx?.ToString()),
                new KeyValuePair<string, string>("time_in_force", order.TimeInForce),
                new KeyValuePair<string, string>("trigger_by",order.TriggerBy?.ToString()),
                new KeyValuePair<string, string>("reduce_only",order.ReduceOnly?.ToString()),
                new KeyValuePair<string, string>("close_on_trigger",order.CloseOnTrigger?.ToString()),
                new KeyValuePair<string, string>("order_link_id",order.OrderLinkId),
                new KeyValuePair<string, string>("take_profit",order.TakeProfit?.ToString()),
                new KeyValuePair<string, string>("stop_loss",order.StopLoss?.ToString()),
                new KeyValuePair<string, string>("tp_trigger_by",order.TpTriggerBy),
                new KeyValuePair<string, string>("sl_trigger_by",order.SlTriggerBy),
                new KeyValuePair<string, string>("position_idx",order.PositionIdx.ToString()),
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)ConditionalApiInstance.LinearConditionalNew(order.Symbol, order.Side, order.OrderType, order.Qty, order.Price, order.BasePrice, order.StopPx, order.TimeInForce, order.TriggerBy, order.ReduceOnly, order.CloseOnTrigger, order.OrderLinkId, order.TakeProfit, order.StopLoss, order.TpTriggerBy, order.SlTriggerBy, order.PositionIdx);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<OrderResBase>();
            if (obj.Result == null) throw new ApiResultException("OrderNewConditional", jObject);
            var data = ((JObject)obj.Result).ToObject<ConditionalRes>();
            return data;
        }

        public ConditionalRes AmendStopOrder(ConditionalRes order)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("stop_order_id", order.StopOrderId),
                new KeyValuePair<string, string>("order_link_id", order.OrderLinkId),
                new KeyValuePair<string, string>("symbol", order.Symbol),
                new KeyValuePair<string, string>("p_r_qty", order.Qty?.ToString()),
                new KeyValuePair<string, string>("p_r_price", order.Price?.ToString()),
                new KeyValuePair<string, string>("p_r_trigger_price", order.StopPx?.ToString()),
                new KeyValuePair<string, string>("take_profit",order.TakeProfit?.ToString()),
                new KeyValuePair<string, string>("stop_loss",order.StopLoss?.ToString()),
                new KeyValuePair<string, string>("tp_trigger_by",order.TpTriggerBy),
                new KeyValuePair<string, string>("sl_trigger_by",order.SlTriggerBy),
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)ConditionalApiInstance.LinearConditionalReplace(order.Symbol, order.StopOrderId, order.OrderLinkId, order.Qty, order.Price, order.StopPx, order.TakeProfit, order.StopLoss, order.TpTriggerBy, order.SlTriggerBy);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<OrderResBase>();
            if (obj.Result == null) throw new ApiResultException("OrderAmendStop", jObject);
            var data = ((JObject)obj.Result).ToObject<ConditionalRes>();
            return data;
        }

        public List<PositionInfo> GetPositionList(string symbol, out PositionInfo buyPosition, out PositionInfo sellPosition)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", symbol)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)PositionsApiInstance.LinearPositionsMyPosition(symbol);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<Position>();
            if (obj.Result == null) throw new ApiResultException("GetPosition", jObject);
            var data = ((JArray)obj.Result).ToObject<List<PositionInfo>>();
            buyPosition = null; sellPosition = null;
            foreach (var p in data)
            {
                if (p.Side == "Buy") buyPosition = p;
                else if (p.Side == "Sell") sellPosition = p;
            }
            return data;
        }

        public void SetPositionStop(string symbol, string side, decimal? takeProfit = null, decimal? stopLoss = null, decimal? trailingStop = null, string tpTriggerBy = null, string slTriggerBy = null, decimal? slSize = null, decimal? tpSize = null, int? positionIdx = null)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", symbol),
                new KeyValuePair<string, string>("side", side),
                new KeyValuePair<string, string>("take_profit", takeProfit?.ToString()),
                new KeyValuePair<string, string>("stop_loss", stopLoss?.ToString()),
                new KeyValuePair<string, string>("trailing_stop", trailingStop?.ToString()),
                new KeyValuePair<string, string>("tp_trigger_by", tpTriggerBy?.ToString()),
                new KeyValuePair<string, string>("sl_trigger_by", slTriggerBy?.ToString()),
                new KeyValuePair<string, string>("sl_size", slSize?.ToString()),
                new KeyValuePair<string, string>("tp_size", tpSize?.ToString()),
                new KeyValuePair<string, string>("position_idx", positionIdx?.ToString()),
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)PositionsApiInstance.LinearPositionsTradingStop(symbol, side, takeProfit, stopLoss, trailingStop, tpTriggerBy, slTriggerBy, slSize, tpSize, positionIdx);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<TradingStopBase>();
            if (obj.RetCode != 0) throw new ApiResultException("SetPositionStopLoss", jObject);
        }

        public WalletBalance GetWalletBalance(string coin = null)
        {
            RequestCount++;
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("coin", coin)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)WalletApiInstance.WalletGetBalance(coin);
            ServerTime = DateTimeExtensions.FromJavaMilliseconds((long)((decimal)jObject["time_now"] * 1000));
            var obj = jObject.ToObject<WalletBalanceBase>();
            if (obj.Result == null) throw new ApiResultException("GetWalletBalance", jObject);
            var balance = ((JObject)obj.Result)[coin].ToObject<WalletBalance>();
            return balance;
        }

        public static List<KlineRes> ConvertBinSize(List<KlineRes> list, int size, int offset)
        {
            if (size == 1) return list;
            int count = list.Count;
            var resultList = new List<KlineRes>();
            int i = 0;
            while (i < count)
            {
                if (list[i].Timestamp().Value.Hour % size != offset)
                {
                    i++;
                    continue;
                }
                var openTime = list[i].OpenTime;
                var open = list[i].Open;
                var high = list[i].High;
                var low = list[i].Low;
                var close = list[i].Close;
                var volume = list[i].Volume;
                for (int j = i + 1; j < i + size && j < count; j++)
                {
                    if (high < list[j].High) high = list[j].High;
                    if (low > list[j].Low) low = list[j].Low;
                    close = list[j].Close;
                    volume += list[j].Volume;
                }
                resultList.Add(new KlineRes
                {
                    OpenTime = openTime,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume
                });
                i += size;
            }
            return resultList;
        }

    }

    public static class DateTimeExtensions
    {
        private static readonly DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime FromJavaMilliseconds(long milliseconds)
        {
            return Jan1st1970.AddMilliseconds(milliseconds);
        }

        public static long ToJavaMilliseconds(this DateTime source)
        {
            return (long)(source - Jan1st1970).TotalMilliseconds;
        }
    }

    public static class KlineResExtensions
    {
        public static DateTime? Timestamp(this KlineRes source)
        {
            if (source.OpenTime == null) return null;
            return DateTimeExtensions.FromJavaMilliseconds((long)(source.OpenTime.Value * 1000));
        }
    }

}
