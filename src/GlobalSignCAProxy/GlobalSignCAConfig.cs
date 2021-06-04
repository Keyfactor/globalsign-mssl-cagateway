using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Order;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using query = Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Query;
using order = Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Order;

namespace Keyfactor.Extensions.AnyGateway.GlobalSign
{
    public class GlobalSignCAConfig
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool IsTest { get; set; }
        public int PickupRetries { get; set;}
        public int PickupDelay { get; set; }
        public string Username { get; set; } 
        public string Password { get; set; }

        public string GetUrl(GlobalSignServiceType queryType)
        {
            switch (queryType)
            {
                case GlobalSignServiceType.ORDER:
                    return IsTest ? Constants.ORDER_TEST_URL : Constants.ORDER_PROD_URL;
                    
                case GlobalSignServiceType.QUERY:
                    return IsTest ? Constants.QUERY_TEST_URL : Constants.QUERY_PROD_URL;
                default:
                    throw new ArgumentException($"Invalid value ({queryType}) for queryType argument");
            }
        }

        public query.AuthToken GetQueryAuthToken()
        {
            return new query.AuthToken { UserName = this.Username, Password = this.Password };
        }

        public order.AuthToken GetOrderAuthToken()
        {
            return new order.AuthToken { UserName = this.Username, Password = this.Password };
        }
    }
    public enum GlobalSignServiceType
    { 
        ORDER,
        QUERY
    }

    public class ClientCertificate
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public StoreLocation StoreLocation { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public StoreName StoreName { get; set; }
        public string Thumbprint { get; set; }
    }
}
