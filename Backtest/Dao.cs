using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using Valloon.Stock.Indicators;
using Valloon.Trading;

namespace Valloon.Trading.Backtest
{
    public class Dao
    {
        public const string DB_FILENAME = @"data.db";
        public const string DATETIME_FORMAT = @"yyyy-MM-dd HH:mm:ss";
        public const string DATE_FORMAT = @"yyyy-MM-dd";
        public const string TIME_FORMAT = @"HH:mm";
        public static SQLiteConnection Connection { get; }
        public static Boolean Encrypted;

        static Dao()
        {
            FileInfo fileInfo = new FileInfo(DB_FILENAME);
            if (!fileInfo.Exists) throw new FileNotFoundException("Can not find database file - " + DB_FILENAME);
            string connectionstring = @"Data Source=" + DB_FILENAME + ";Version=3";
            SQLiteConnection con = new SQLiteConnection(connectionstring);
            con.Open();
            using (SQLiteCommand command = con.CreateCommand())
            {
                command.CommandText = "PRAGMA encoding";
                object result = command.ExecuteScalar();
            }
            Encrypted = false;
            Connection = con;
        }

        public static Boolean Close()
        {
            try
            {
                Connection.Close();
                Connection.Dispose();
                return true;
            }
            catch { }
            return false;
        }

        public static List<CandleQuote> SelectAll(string symbol, string binSize)
        {
            using (SQLiteCommand command = Connection.CreateCommand())
            {
                command.CommandText = $"SELECT * FROM {symbol.ToLower()}_{binSize} ORDER BY timestamp";
                SQLiteDataReader dr = command.ExecuteReader();
                var list = new List<CandleQuote>();
                while (dr.Read())
                {
                    try
                    {
                        var m = new CandleQuote
                        {
                            Timestamp = ParseDateTimeString(GetValue<string>(dr["timestamp"])),
                            Open = GetValue<int>(dr["open"]),
                            High = GetValue<int>(dr["high"]),
                            Low = GetValue<int>(dr["low"]),
                            Close = GetValue<int>(dr["close"]),
                            Volume = GetValue<long>(dr["volume"]),
                        };
                        list.Add(m);
                    }
                    catch (FormatException e)
                    {
                        Console.WriteLine(GetValue<string>(dr["timestamp"]) + " \t " + e.Message);
                    }
                }
                return list;
            }
        }

        public static int Insert(string symbol, string binSize, CandleQuote m)
        {
            using (var command = Connection.CreateCommand())
            {
                command.CommandText = $"INSERT INTO {symbol.ToLower()}_{binSize}(timestamp,date,time,open,high,low,close,volume) VALUES(@timestamp,@date,@time,@open,@high,@low,@close,@volume)";
                command.Parameters.Add("timestamp", System.Data.DbType.String).Value = ToDateTimestring(m.Timestamp);
                command.Parameters.Add("date", System.Data.DbType.String).Value = m.Timestamp.ToString("yyyy-MM-dd");
                command.Parameters.Add("time", System.Data.DbType.String).Value = m.Timestamp.ToString("HH:mm");
                command.Parameters.Add("open", System.Data.DbType.Int32).Value = m.Open;
                command.Parameters.Add("high", System.Data.DbType.Int32).Value = m.High;
                command.Parameters.Add("low", System.Data.DbType.Int32).Value = m.Low;
                command.Parameters.Add("close", System.Data.DbType.Int32).Value = m.Close;
                command.Parameters.Add("volume", System.Data.DbType.Int32).Value = m.Volume;
                return command.ExecuteNonQuery();
            }
        }

        //public static void Encrypt()
        //{
        //    connection.ChangePassword(DB_PASSWORD);
        //}

        //public static void Decrypt()
        //{
        //    connection.ChangePassword("");
        //}

        public static T GetValue<T>(object obj)
        {
            Type type = typeof(T);
            if (obj == null || obj == DBNull.Value)
            {
                return default; // returns the default value for the type
            }
            else if (type == typeof(DateTime))
            {
                return (T)(Object)DateTime.ParseExact((string)obj, DATETIME_FORMAT, CultureInfo.CurrentCulture);
            }
            else
            {
                return (T)Convert.ChangeType(obj, typeof(T));
            }
        }

        public static string ToDateString(DateTime dt)
        {
            return dt.ToString(DATE_FORMAT);
        }

        public static DateTime ParseDateString(string s)
        {
            return DateTime.ParseExact(s, DATE_FORMAT, CultureInfo.CurrentCulture);
        }

        public static string ToDateTimestring(DateTime dt)
        {
            return dt.ToString(DATETIME_FORMAT);
        }

        public static DateTime ParseDateTimeString(string s)
        {
            return DateTime.ParseExact(s, DATETIME_FORMAT, CultureInfo.CurrentCulture);
        }

    }
}
