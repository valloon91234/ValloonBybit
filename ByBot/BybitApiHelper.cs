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
using System.Threading.Tasks;

/**
 * @author Valloon Project
 * @version 1.0 @2020-03-25
 */
namespace Valloon.ByBot
{
    class BybitApiHelper
    {
        private static readonly DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long UtcMilliseconds
        {
            get
            {
                return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
            }
        }

        private static byte[] Hmacsha256(byte[] keyByte, byte[] messageBytes)
        {
            using (var hash = new HMACSHA256(keyByte))
            {
                return hash.ComputeHash(messageBytes);
            }
        }

        private static string ByteArrayToString(byte[] ba)
        {
            var hex = new StringBuilder(ba.Length * 2);
            foreach (var b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }

        public const string SYMBOL = "BTCUSD";
        public const string COIN_BTC = "BTC";

        public readonly string API_KEY;
        public readonly string API_SECRET;

        private readonly KlineApi KLineApiInstance;
        private readonly MarketApi MarketApiInstance;
        private readonly WalletApi WalletApiInstance;
        private readonly OrderApi OrderApiInstance;
        private readonly ConditionalApi ConditionalApiInstance;
        private readonly PositionsApi PositionsApiInstance;

        public DateTime ServerTime { get; set; }
        public long TimeDistanceMilliseconds { get; set; }

        public long Timestamp
        {
            get
            {
                return UtcMilliseconds - TimeDistanceMilliseconds;
            }
        }

        public BybitApiHelper(string apiKey, string apiSecret, bool testnet = false)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            API_KEY = apiKey;
            API_SECRET = apiSecret;
            {
                CommonApi api = new CommonApi();
                JObject jObject = (JObject)api.CommonGetWithHttpInfo().Data;
                long serverMilliseconds = (long)(decimal)jObject["time_now"] * 1000;
                ServerTime = (new DateTime(1970, 1, 1)).AddMilliseconds(serverMilliseconds);
                TimeDistanceMilliseconds = UtcMilliseconds - serverMilliseconds;
                Logger.WriteLine($"time_distance = {TimeDistanceMilliseconds / 1000m} seconds", ConsoleColor.DarkGray);
            }
            if (!testnet) Configuration.Default.BasePath = "https://api.bybit.com";
            KLineApiInstance = new KlineApi();
            MarketApiInstance = new MarketApi();
            WalletApiInstance = new WalletApi();
            OrderApiInstance = new OrderApi();
            ConditionalApiInstance = new ConditionalApi();
            PositionsApiInstance = new PositionsApi();
        }

        public int GetRecentVolume(int minutes = 5)
        {
            long from = Timestamp / 1000 - (minutes + 1) * 60;
            JObject jObject = (JObject)KLineApiInstance.KlineGet(SYMBOL, "1", from);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<KlineBase>();
            List<KlineRes> resultList = obj.Result;
            List<int> volumeList = new List<int>();
            int count = resultList.Count - 1;
            for (int i = 0; i < count; i++)
            {
                var item = resultList[i];
                volumeList.Add(int.Parse(item.Volume, CultureInfo.InvariantCulture));
            }
            return (int)volumeList.Average();
        }

        public SymbolTickInfo GetTicker()
        {
            JObject jObject = (JObject)MarketApiInstance.MarketSymbolInfo(SYMBOL);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<SymbolInfoBase>();
            List<SymbolTickInfo> resultList = obj.Result;
            foreach (var item in resultList)
            {
                return item;
            }
            return null;
        }

        private string CreateSignature(List<KeyValuePair<string, string>> paramList)
        {
            string timestampString = Timestamp.ToString();
            paramList.Add(new KeyValuePair<string, string>("api_key", API_KEY));
            paramList.Add(new KeyValuePair<string, string>("timestamp", timestampString));
            string message = "";
            var paramListOrdered = paramList.OrderBy(n => n.Key);
            foreach (var param in paramListOrdered)
            {
                message += $"&{param.Key}={param.Value}";
            }
            message = message.Substring(1);
            var signatureBytes = Hmacsha256(Encoding.UTF8.GetBytes(API_SECRET), Encoding.UTF8.GetBytes(message));
            string sign = ByteArrayToString(signatureBytes);
            Configuration.Default.ApiKey["api_key"] = API_KEY;
            Configuration.Default.ApiKey["timestamp"] = timestampString;
            Configuration.Default.ApiKey["sign"] = sign;
            return message;
        }

        public WalletBalance GetWalletBalance(string coin = COIN_BTC)
        {
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("coin", coin)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)WalletApiInstance.WalletGetBalance(coin);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<WalletBalanceBase>();
            if (obj.Result == null) throw new ApiResultException("GetWalletBalance", jObject);
            var balance = ((JObject)obj.Result)[coin].ToObject<WalletBalance>();
            return balance;
        }

        public List<OrderRes> GetActiveOrders()
        {
            int limit = 50;
            string orderStatus = "New,PartiallyFilled";
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", SYMBOL),
                new KeyValuePair<string, string>("limit", limit.ToString()),
                new KeyValuePair<string, string>("order_status", orderStatus)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.OrderGetOrders(null, null, SYMBOL, null, null, limit, orderStatus);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<OrderListBase>();
            if (obj.Result == null) throw new ApiResultException("GetActiveOrders", jObject);
            var data = ((JObject)obj.Result).ToObject<OrderListData>();
            return data.Data;
        }

        public QueryOrderRes GetQueryActiveOrder(string orderId)
        {
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("order_id", orderId),
                new KeyValuePair<string, string>("symbol", SYMBOL)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.OrderQuery(orderId, SYMBOL);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<QueryOrderBase>();
            if (obj.Result == null) throw new ApiResultException("GetQueryActiveOrder", jObject);
            var result = ((JObject)obj.Result).ToObject<QueryOrderRes>();
            return result;
        }

        public OrderRes CancelActiveOrder(string orderId)
        {
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", SYMBOL),
                new KeyValuePair<string, string>("order_id", orderId)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.OrderCancelV2(orderId, SYMBOL);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<OrderCancelBase>();
            if (obj.Result == null) return null;
            var data = ((JObject)obj.Result).ToObject<OrderRes>();
            return data;
        }

        public List<OrderCancelAllRes> CancelAllActiveOrders()
        {
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", SYMBOL)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.OrderCancelAll(SYMBOL);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<OrderCancelAllBase>();
            if (obj.Result == null) return null;
            var data = ((JArray)obj.Result).ToObject<List<OrderCancelAllRes>>();
            return data;
        }

        public OrderRes OrderNewLimit(string side, int qty, decimal price, bool reduceOnly = false, bool closeOnTrigger = false)
        {
            const string orderType = "Limit";
            const string timeInForce = "GoodTillCancel";
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("side", side),
                new KeyValuePair<string, string>("symbol", SYMBOL),
                new KeyValuePair<string, string>("order_type", orderType),
                new KeyValuePair<string, string>("qty", qty.ToString()),
                new KeyValuePair<string, string>("price", price.ToString()),
                new KeyValuePair<string, string>("time_in_force", timeInForce),
                new KeyValuePair<string, string>("reduce_only",reduceOnly.ToString()),
                new KeyValuePair<string, string>("close_on_trigger",closeOnTrigger.ToString())
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.OrderNewV2(side, SYMBOL, orderType, qty, timeInForce, price, null, null, reduceOnly, closeOnTrigger);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<OrderResBase>();
            if (obj.Result == null) throw new ApiResultException("OrderNewLimit", jObject);
            var data = ((JObject)obj.Result).ToObject<OrderRes>();
            return data;
        }

        public OrderRes OrderNewMarket(string side, int qty, bool closeOnTrigger)
        {
            const string orderType = "Market";
            const string timeInForce = "GoodTillCancel";
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("side", side),
                new KeyValuePair<string, string>("symbol", SYMBOL),
                new KeyValuePair<string, string>("order_type", orderType),
                new KeyValuePair<string, string>("qty", qty.ToString()),
                new KeyValuePair<string, string>("time_in_force",timeInForce),
                new KeyValuePair<string, string>("close_on_trigger",closeOnTrigger.ToString())
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)OrderApiInstance.OrderNewV2(side, SYMBOL, orderType, qty, timeInForce, null, null, null, null, closeOnTrigger);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<OrderResBase>();
            if (obj.Result == null) throw new ApiResultException("OrderNewMarket", jObject);
            var data = ((JObject)obj.Result).ToObject<OrderRes>();
            return data;
        }

        public ConditionalRes OrderNewStopMarket(string side, int qty, decimal basePrice, decimal stopPx, bool closeOnTrigger)
        {
            const string orderType = "Market";
            const string timeInForce = "GoodTillCancel";
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("side", side),
                new KeyValuePair<string, string>("symbol", SYMBOL),
                new KeyValuePair<string, string>("order_type", orderType),
                new KeyValuePair<string, string>("qty", qty.ToString()),
                new KeyValuePair<string, string>("base_price", basePrice.ToString()),
                new KeyValuePair<string, string>("stop_px", stopPx.ToString()),
                new KeyValuePair<string, string>("time_in_force",timeInForce),
                new KeyValuePair<string, string>("close_on_trigger",closeOnTrigger.ToString())
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)ConditionalApiInstance.ConditionalNew(side, SYMBOL, orderType, qty, basePrice, stopPx, timeInForce, null, null, closeOnTrigger);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<ConditionalBase>();
            if (obj.Result == null) throw new ApiResultException("OrderNewStopMarket", jObject);
            var data = ((JObject)obj.Result).ToObject<ConditionalRes>();
            return data;
        }

        public PositionInfo GetPosition()
        {
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", SYMBOL)
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)PositionsApiInstance.PositionsMyPositionV2(SYMBOL);
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<Position>();
            if (obj.Result == null) throw new ApiResultException("GetPosition", jObject);
            var data = ((JObject)obj.Result).ToObject<PositionInfo>();
            return data;
        }

        public TradingStopRes SetPositionStopLoss(decimal stopLoss)
        {
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("symbol", SYMBOL),
                new KeyValuePair<string, string>("stop_loss", stopLoss.ToString())
            };
            CreateSignature(paramList);
            JObject jObject = (JObject)PositionsApiInstance.PositionsTradingStop(SYMBOL, null, stopLoss.ToString());
            TimeDistanceMilliseconds = UtcMilliseconds - (long)((decimal)jObject["time_now"] * 1000);
            var obj = jObject.ToObject<TradingStopBase>();
            if (obj.Result == null) throw new ApiResultException("SetPositionStopLoss", jObject);
            var data = ((JObject)obj.Result).ToObject<TradingStopRes>();
            return data;
        }

        public void Test()
        {
            var apiInstance = new APIkeyApi();
            try
            {
                // Get account api-key information.
                Object result = apiInstance.APIkeyInfo();
                Console.WriteLine(result);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception when calling APIkeyApi.APIkeyInfo: " + e.Message);
            }
            Console.ReadKey(false);
        }
    }
}
