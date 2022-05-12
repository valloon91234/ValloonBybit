using System;
using System.Linq;
using System.Text;

/**
 * @author Valloon Project
 * @version 1.0 @2020-03-03
 */
namespace Valloon.Utils
{
    public static class StringUtils
    {
        public static string GetNumbers(String input)
        {
            return new string(input.Where(c => char.IsDigit(c)).ToArray());
        }

        public static byte[] XorBytes(byte[] input, byte[] key)
        {
            int length = input.Length;
            int keyLength = key.Length;
            for (int i = 0; i < length; i++)
            {
                input[i] ^= key[i % keyLength];
            }
            return input;
        }

        public static string ToHexString(byte[] bytes)
        {
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                hex.Append(b.ToString("X2"));
            return hex.ToString();
        }

        public static byte[] ParseHexString(string hex)
        {
            int hexLength = hex.Length;
            byte[] bytes = new byte[hexLength / 2];
            for (int i = 0; i < hexLength; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

    }

    public static class StringExtensions
    {
        public static bool ContainsIgnoreCase(this string source, string toCheck)
        {
            return source?.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

}


