using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

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

            MoveWindow(16, 16, 960, 140);
            Paxg.Run();

            Console.WriteLine($"\nCompleted. Press any key to exit... ");
            Console.ReadKey(false);
        }
    }
}
