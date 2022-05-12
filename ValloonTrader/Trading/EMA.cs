using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valloon.Trading
{
    public class EMA
    {
        private readonly double alpha;
        private double lastAverage = double.NaN;

        public EMA(int lookBack)
        {
            this.alpha = 2f / (lookBack + 1);
        }

        public EMA(double alpha)
        {
            this.alpha = alpha;
        }

        public double NextValue(double value)
        {
            //lastAverage = double.IsNaN(lastAverage) ? value : (value - lastAverage) * alpha + lastAverage;
            lastAverage = double.IsNaN(lastAverage) ? value : lastAverage * (1 - alpha) + value * alpha;
            return lastAverage;
        }
    }
}
