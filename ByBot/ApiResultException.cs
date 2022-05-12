using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valloon.ByBot
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
    }
}
