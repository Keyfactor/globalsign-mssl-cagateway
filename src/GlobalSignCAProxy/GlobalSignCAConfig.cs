// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using System;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using query = Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Query;
using order = Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Order;
using System.Diagnostics.Contracts;

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

		public string SyncStartDate { get; set; }
		public int SyncIntervalDays { get; set; }

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
