using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Valloon.Trading.Backtest
{
    internal class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public static void MoveWindow(int X, int Y, int nWidth, int nHeight)
        {
            MoveWindow(GetConsoleWindow(), X, Y, nWidth, nHeight, true);
        }

        static void Main(string[] args)
        {
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            PSar.Run();
            //SMA2.Run();

            Console.WriteLine($"\nCompleted. Press any key to exit... ");
            Console.ReadKey(false);
        }
    }
}
