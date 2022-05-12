using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/**
 * @author Valloon Project
 * @version 1.0 @2020-03-03
 */
namespace Valloon.ByBot
{
    public class Config
    {
        public static readonly string FILENAME = "config.ini";
        public static readonly string KEY_USERNAME = "USERNAME";
        public static readonly string KEY_API_KEY = "API_KEY";
        public static readonly string KEY_API_SECRET = "API_SECRET";
        public static readonly string KEY_TESTNET_MODE = "TESTNET_MODE";
        public static readonly string KEY_LIMIT_LOWER = "LIMIT_LOWER";
        public static readonly string KEY_LIMIT_LOWER_CANCEL = "LIMIT_LOWER_CANCEL";
        public static readonly string KEY_LIMIT_HIGHER = "LIMIT_HIGHER";
        public static readonly string KEY_LIMIT_HIGHER_CANCEL = "LIMIT_HIGHER_CANCEL";
        public static readonly string KEY_LIMIT_HIGHER_FORCE = "LIMIT_HIGHER_FORCE";
        public static readonly string KEY_LIMIT_MARK = "LIMIT_MARK";
        public static readonly string KEY_LIMIT_MARK_CANCEL = "LIMIT_MARK_CANCEL";
        public static readonly string KEY_PROFIT_TARGET = "PROFIT_TARGET";
        public static readonly string KEY_STAIRS = "STAIRS";
        public static readonly string KEY_STAIRS_ARRAY = "STAIRS_ARRAY";
        public static readonly string KEY_INVEST_ARRAY = "INVEST_ARRAY";
        public static readonly string KEY_INVEST_RATIO = "INVEST_RATIO";
        public static readonly string KEY_STOP_LOSS = "STOP_LOSS";
        public static readonly string KEY_DIRECTION = "DIRECTION";
        public static readonly string KEY_RESET_DISTANCE = "RESET_DISTANCE";
        public static readonly string KEY_EXIT = "EXIT";

        public static bool CheckExist()
        {
            return new FileInfo(FILENAME).Exists;
        }

        public static string Get(string key)
        {
            try
            {
                string[] lines = File.ReadAllLines(FILENAME, Encoding.UTF8);
                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || line.StartsWith("//")) continue;
                    string[] array = line.Split(new Char[] { '=' }, 2);
                    string name = array[0];
                    string value = array.Length > 1 ? array[1] : null;
                    if (name == key)
                    {
                        if (string.IsNullOrWhiteSpace(value))
                            return null;
                        return value.Trim();
                    }
                }
            }
            catch { }
            return null;
        }

        public static bool Write(string key, string value)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(FILENAME);
                if (fileInfo.Exists)
                {
                    string[] lines = File.ReadAllLines(FILENAME, System.Text.Encoding.UTF8);
                    using (StreamWriter file = new StreamWriter(FILENAME, false, System.Text.Encoding.UTF8))
                    {
                        bool foundKey = false;
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("#") || line.StartsWith("//"))
                            {
                                file.WriteLine(line);
                                continue;
                            }
                            string[] array = line.Split(new Char[] { '=' }, 2);
                            string name = array[0];
                            string value0 = array.Length > 1 ? array[1] : null;
                            if (name == key)
                            {
                                foundKey = true;
                                file.WriteLine(name + "=" + value);
                            }
                            else
                            {
                                file.WriteLine(line);
                            }
                        }
                        if (!foundKey) file.WriteLine(key + "=" + value);
                    }
                }
                else
                {
                    using (StreamWriter file = new StreamWriter(FILENAME, false, System.Text.Encoding.UTF8))
                    {
                        file.WriteLine(key + "=" + value);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Load()
        {
            try
            {
                if (!CheckExist())
                {
                    Logger.WriteLine($"\"{Config.FILENAME}\" not found.", ConsoleColor.Red);
                    return false;
                }
                GlobalParam.API_KEY = Get(KEY_API_KEY);
                if (GlobalParam.API_KEY == null)
                {
                    Logger.WriteLine("API_KEY not found in \"connig.ini\".", ConsoleColor.Red); ;
                    return false;
                }
                GlobalParam.API_SECRET = Get(KEY_API_SECRET);
                if (GlobalParam.API_SECRET == null)
                {
                    Logger.WriteLine("API_SECRET not found in \"connig.ini\".", ConsoleColor.Red); ;
                    return false;
                }
                GlobalParam.USERNAME = Get(Config.KEY_USERNAME);
                string testnetMode = Get(Config.KEY_TESTNET_MODE);
                GlobalParam.TESTNET_MODE = testnetMode != "0";
                string limitLower = Get(Config.KEY_LIMIT_LOWER);
                string limitLowerCancel = Get(Config.KEY_LIMIT_LOWER_CANCEL);
                string limitHigher = Get(Config.KEY_LIMIT_HIGHER);
                string limitHigherCancel = Get(Config.KEY_LIMIT_HIGHER_CANCEL);
                string limitHigherForce = Get(Config.KEY_LIMIT_HIGHER_FORCE);
                string limitMark = Get(Config.KEY_LIMIT_MARK);
                string limitMarkCancel = Get(Config.KEY_LIMIT_MARK_CANCEL);
                string profitTarget = Get(Config.KEY_PROFIT_TARGET);
                string stairs = Get(Config.KEY_STAIRS);
                string stairsArray = Get(Config.KEY_STAIRS_ARRAY);
                string investArray = Get(Config.KEY_INVEST_ARRAY);
                string investRatio = Get(Config.KEY_INVEST_RATIO);
                string stopLoss = Get(Config.KEY_STOP_LOSS);
                string direction = Get(Config.KEY_DIRECTION);
                string resetDistance = Get(Config.KEY_RESET_DISTANCE);
                string exit = Get(Config.KEY_EXIT);
                if (limitLower != null) GlobalParam.LIMIT_LOWER = Convert.ToInt32(limitLower, CultureInfo.InvariantCulture);
                if (limitLowerCancel != null) GlobalParam.LIMIT_LOWER_CANCEL = Convert.ToInt32(limitLowerCancel, CultureInfo.InvariantCulture);
                if (limitHigher != null) GlobalParam.LIMIT_HIGHER = Convert.ToInt32(limitHigher, CultureInfo.InvariantCulture);
                if (limitHigherCancel != null) GlobalParam.LIMIT_HIGHER_CANCEL = Convert.ToInt32(limitHigherCancel, CultureInfo.InvariantCulture);
                if (limitHigherForce != null) GlobalParam.LIMIT_HIGHER_FORCE = Convert.ToInt32(limitHigherForce, CultureInfo.InvariantCulture) != 0;
                if (limitMark != null) GlobalParam.LIMIT_MARK = Convert.ToInt32(limitMark, CultureInfo.InvariantCulture);
                if (limitMarkCancel != null) GlobalParam.LIMIT_MARK_CANCEL = Convert.ToInt32(limitMarkCancel, CultureInfo.InvariantCulture);
                if (profitTarget != null) GlobalParam.ProfitTarget = Convert.ToDecimal(profitTarget, CultureInfo.InvariantCulture);
                if (stairs != null) GlobalParam.Stairs = Convert.ToInt32(stairs, CultureInfo.InvariantCulture);
                if (stairsArray != null) GlobalParam.StairsArray = Array.ConvertAll(stairsArray.Split(','), s => int.Parse(s.Trim(), CultureInfo.InvariantCulture));
                if (investArray != null) GlobalParam.InvestArray = Array.ConvertAll(investArray.Split(','), s => decimal.Parse(s.Trim(), CultureInfo.InvariantCulture));
                if (investRatio != null) GlobalParam.InvestRatio = decimal.Parse(investRatio, CultureInfo.InvariantCulture);
                if (stopLoss != null) GlobalParam.StopLoss = decimal.Parse(stopLoss, CultureInfo.InvariantCulture);
                if (direction != null)
                {
                    direction = direction.ToUpper();
                    if (direction == "BUY" || direction == "LONG") GlobalParam.Direction = 1;
                    else if (direction == "SELL" || direction == "SHORT") GlobalParam.Direction = 2;
                }
                if (resetDistance != null) GlobalParam.ResetDistance = Convert.ToInt32(resetDistance, CultureInfo.InvariantCulture);
                if (exit != null) GlobalParam.Exit = Convert.ToInt32(exit, CultureInfo.InvariantCulture);
                Logger.WriteLine("username = " + GlobalParam.USERNAME);
                Logger.WriteLine("api_key = " + GlobalParam.API_KEY);
                Logger.WriteLine("testnet_mode = " + GlobalParam.TESTNET_MODE.ToString().ToLower());
                Logger.WriteLine("limit_lower = " + GlobalParam.LIMIT_LOWER);
                Logger.WriteLine("limit_lower_cancel = " + GlobalParam.LIMIT_LOWER_CANCEL);
                Logger.WriteLine("limit_higher = " + GlobalParam.LIMIT_HIGHER);
                Logger.WriteLine("limit_higher_cancel = " + GlobalParam.LIMIT_HIGHER_CANCEL);
                Logger.WriteLine("limit_higher_force = " + GlobalParam.LIMIT_HIGHER_FORCE);
                Logger.WriteLine("limit_mark = " + GlobalParam.LIMIT_MARK);
                Logger.WriteLine("limit_mark_cancel = " + GlobalParam.LIMIT_MARK_CANCEL);
                Logger.WriteLine("profit_target = " + GlobalParam.ProfitTarget);
                Logger.WriteLine("stairs = " + GlobalParam.Stairs);
                Logger.WriteLine("stairs_array = " + string.Join(", ", GlobalParam.StairsArray));
                Logger.WriteLine("invest_array = " + string.Join(", ", GlobalParam.InvestArray));
                Logger.WriteLine("invest_ratio = " + GlobalParam.InvestRatio);
                Logger.WriteLine("stop_loss = " + GlobalParam.StopLoss);
                if (GlobalParam.USERNAME == null) GlobalParam.USERNAME = GlobalParam.API_KEY;
                if (GlobalParam.StairsArray.Length < GlobalParam.Stairs)
                {
                    Logger.WriteLine("Config error : length of stairs_array < stairs");
                    return false;
                }
                if (GlobalParam.InvestArray.Length < GlobalParam.Stairs)
                {
                    Logger.WriteLine("Config error : length of invest_array < stairs");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Error in reading config. " + ex.Message);
                return false;
            }
        }
    }
}