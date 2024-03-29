﻿using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

/**
 * @author Valloon Present
 * @version 2020-03-03
 */
namespace Valloon.Utils
{
    public class SocksHttpWebResponse : WebResponse
    {

        #region Member Variables

        private WebHeaderCollection _httpResponseHeaders;
        private string _responseContent;

        #endregion

        #region Constructors

        public SocksHttpWebResponse(string httpResponseMessage)
        {
            SetHeadersAndResponseContent(httpResponseMessage);
        }

        #endregion

        #region WebResponse Members

        public override Stream GetResponseStream()
        {
            return ResponseContent.Length == 0 ? Stream.Null : new MemoryStream(Encoding.UTF8.GetBytes(ResponseContent));
        }

        public override void Close() { /* the base implementation throws an exception */ }

        public override WebHeaderCollection Headers
        {
            get
            {
                if (_httpResponseHeaders == null)
                {
                    _httpResponseHeaders = new WebHeaderCollection();
                }
                return _httpResponseHeaders;
            }
        }

        public override long ContentLength
        {
            get
            {
                return ResponseContent.Length;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        #endregion

        #region Methods

        private void SetHeadersAndResponseContent(string responseMessage)
        {
            // the HTTP headers can be found before the first blank line
            var indexOfFirstBlankLine = responseMessage.IndexOf("\r\n\r\n");

            var headers = responseMessage.Substring(0, indexOfFirstBlankLine);
            var headerValues = headers.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            // ignore the first line in the header since it is the HTTP response code
            for (int i = 1; i < headerValues.Length; i++)
            {
                var headerEntry = headerValues[i].Split(new[] { ':' });
                Headers.Add(headerEntry[0], headerEntry[1]);
            }

            ResponseContent = responseMessage.Substring(indexOfFirstBlankLine + 4);
        }

        #endregion

        #region Properties

        private string ResponseContent
        {
            get { return _responseContent ?? string.Empty; }
            set { _responseContent = value; }
        }

        #endregion


    }
}
