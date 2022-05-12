using System;

/**
 * @author Valloon Project
 * @version 1.0 @2020-03-06
 */
namespace Valloon.ByBot
{
    static class GlobalParam
    {
        public const string APP_NAME = "ByBot";
        public const string APP_VERSION = "2020.03.27";
        public static string USERNAME { get; set; }
        public static string API_KEY { get; set; }
        public static string API_SECRET { get; set; }
        public static bool TESTNET_MODE { get; set; }
        public static int LIMIT_LOWER { get; set; }
        public static int LIMIT_LOWER_CANCEL { get; set; }
        public static int LIMIT_HIGHER { get; set; }
        public static int LIMIT_HIGHER_CANCEL { get; set; }
        public static bool LIMIT_HIGHER_FORCE { get; set; }
        public static int LIMIT_MARK { get; set; }
        public static int LIMIT_MARK_CANCEL { get; set; }

        public static decimal ProfitTarget { get; set; }
        public static int Stairs { get; set; }
        public static int[] StairsArray { get; set; }
        public static decimal[] InvestArray { get; set; }
        public static decimal InvestRatio { get; set; }
        public static decimal StopLoss { get; set; }
        public static int Direction { get; set; }
        public static int ResetDistance { get; set; }
        public static int Exit { get; set; }

        static GlobalParam()
        {
            GlobalParam.TESTNET_MODE = false;
            GlobalParam.LIMIT_LOWER = 190000;
            GlobalParam.LIMIT_LOWER_CANCEL = 210000;
            GlobalParam.LIMIT_HIGHER = 6000000;
            GlobalParam.LIMIT_HIGHER_CANCEL = 7000000;
            GlobalParam.LIMIT_HIGHER_FORCE = false;
            GlobalParam.LIMIT_MARK = 50;
            GlobalParam.LIMIT_MARK_CANCEL = 100;
            GlobalParam.ProfitTarget = 3;
            //GlobalParam.Stairs = 7;
            //GlobalParam.InvestRatio = 0.225m;
            GlobalParam.StopLoss = 0.3m;
        }
    }
}
