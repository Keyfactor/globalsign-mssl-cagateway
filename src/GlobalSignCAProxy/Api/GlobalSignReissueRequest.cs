// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Query;

namespace Keyfactor.Extensions.AnyGateway.GlobalSign.Api
{
    public class GlobalSignReissueRequest
    {
        private GlobalSignCAConfig Config;

        public GlobalSignReissueRequest(GlobalSignCAConfig config)
        {
            Config = config;
        }
        public string CSR { get; set; }
        public string OrderID { get; set; }
        public string DNSNames { get; set; }
        public QbV1ReIssueRequest Request
        {
            get
            {
                QbV1ReIssueRequest request = new QbV1ReIssueRequest();
                OrderRequestHeader header = new OrderRequestHeader
                {
                    AuthToken = Config.GetQueryAuthToken()
                };
                OrderParameter parameters = new OrderParameter
                {
                    CSR = CSR,
                    DNSNames = DNSNames
                };
                request.TargetOrderID = OrderID;
                request.OrderRequestHeader = header;
                request.OrderParameter = parameters;
                return request;
            }
        }
    }
}
