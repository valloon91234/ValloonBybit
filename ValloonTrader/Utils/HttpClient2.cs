using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;

/**
 * @author Valloon Present
 * @version 2020-03-03
 */
namespace Valloon.Utils
{
    public static class HttpClient2
    {
        public static string HttpGet(string url)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.Timeout = 15000;
            httpWebRequest.ReadWriteTimeout = 15000;
            httpWebRequest.Method = "Get";
            httpWebRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            using (var httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                string Charset = httpWebResponse.CharacterSet;
                using (var receiveStream = httpWebResponse.GetResponseStream())
                using (var streamReader = new StreamReader(receiveStream, Encoding.GetEncoding(Charset)))
                    return streamReader.ReadToEnd();
            }
        }

        public static string HttpPost(string url, string data)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.Timeout = 3000;
            httpWebRequest.ReadWriteTimeout = 3000;
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/x-www-form-urlencoded";
            if (data != null)
            {
                httpWebRequest.ContentLength = data.Length;
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(data);
                }
            }
            using (var httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                string Charset = httpWebResponse.CharacterSet;
                using (var receiveStream = httpWebResponse.GetResponseStream())
                using (var streamReader = new StreamReader(receiveStream, Encoding.GetEncoding(Charset)))
                    return streamReader.ReadToEnd();
            }
        }

    }
}
