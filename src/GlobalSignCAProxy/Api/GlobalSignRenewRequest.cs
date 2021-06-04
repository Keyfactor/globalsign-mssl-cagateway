// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Order;
using System.Collections.Generic;

namespace Keyfactor.Extensions.AnyGateway.GlobalSign.Api
{
    public class GlobalSignRenewRequest : GlobalSignEnrollRequest
    {
        public GlobalSignRenewRequest(GlobalSignCAConfig config) : base(config) { }
        public string RenewalTargetOrderId { get; set; }
        public new BmV2PvOrderRequest Request
        {
            get
            {
                BmV2PvOrderRequest request = new BmV2PvOrderRequest
                {
                    OrderRequestHeader = new OrderRequestHeader { AuthToken = Config.GetOrderAuthToken() },
                    MSSLProfileID = MsslProfileId,
                    MSSLDomainID = MsslDomainId,
                    ContactInfo = new ContactInfo
                    {
                        FirstName = FirstName,
                        LastName = LastName,
                        Phone = Phone,
                        Email = Email
                    }
                };
                if (SANs != null)
                {
                    if (SANs.Count > 0)
                    {
                        List<SANEntry> sans = new List<SANEntry>();
                        foreach (string item in SANs)
                        {
                            SANEntry entry = new SANEntry();
                            entry.SubjectAltName = item;
                            if (item.StartsWith("*"))
                            {
                                entry.SubjectAltName = "13";
                            }
                            else
                            {
                                entry.SubjectAltName = "7";
                            }
                        }
                        request.SANEntries = sans.ToArray();
                    }
                }
                ValidityPeriod validityPeriod = new ValidityPeriod
                {
                    Months = Months
                };
                request.OrderRequestParameter = new OrderRequestParameter
                {
                    ProductCode = ProductCode,
                    OrderKind = OrderKind,
                    Licenses = Licenses,
                    CSR = CSR,
                    RenewalTargetOrderID = RenewalTargetOrderId,
                    ValidityPeriod = validityPeriod,
                };
                return request;
            }
        }

    }
}
