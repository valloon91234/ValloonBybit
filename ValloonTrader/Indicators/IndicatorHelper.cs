using IO.Swagger.Model;
using System.Collections.Generic;
using Valloon.Trading;

namespace Valloon.Indicators
{
    /**
     * https://daveskender.github.io/Stock.Indicators/indicators/
     */
    public class IndicatorHelper
    {
        public static List<Skender.Stock.Indicators.Quote> ToQuote(List<KlineRes> tradeBinList)
        {
            var quoteList = new List<Skender.Stock.Indicators.Quote>();
            foreach (var t in tradeBinList)
            {
                quoteList.Add(new Skender.Stock.Indicators.Quote
                {
                    Date = t.Timestamp().Value,
                    Open = t.Open.Value,
                    High = t.High.Value,
                    Low = t.Low.Value,
                    Close = t.Close.Value,
                    Volume = t.Volume.Value,
                });
            }
            return quoteList;
        }

    }
}
