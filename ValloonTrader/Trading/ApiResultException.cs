using Newtonsoft.Json.Linq;
using System;

namespace Valloon.Trading
{
    class ApiResultException : Exception
    {
        public string Name { get; set; }
        public string ResponseJson { get; set; }
        public ApiResultException(string name, JObject responseJson) : base()
        {
            this.Name = name;
            this.ResponseJson = responseJson.ToString();
        }

        public override string Message => $"ApiResultException on {Name}: {ResponseJson}";
    }
}
