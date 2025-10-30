// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Order;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
							if (string.Equals(item, CommonName, System.StringComparison.OrdinalIgnoreCase))
							{
								Logger.Info($"SAN Entry {item} matches CN, removing from request");
								continue;
							}
							if (string.Equals(item, $"*.{CommonName}", System.StringComparison.OrdinalIgnoreCase))
							{
								Logger.Info($"SAN Entry {item} is the same base domain as the wildcard CN, removing from request");
								continue;
							}
							SANEntry entry = new SANEntry();
							entry.SubjectAltName = item;
							StringBuilder sb = new StringBuilder();
							sb.Append($"Adding SAN entry of type ");
							if (item.StartsWith("*"))
                            {
                                entry.SANOptionType = "13";
								sb.Append("WILDCARD");
							}
                            else
                            {
                                entry.SANOptionType = "7";
								sb.Append("FQDN");
							}
							sb.Append($" and value {item} to request");
							Logger.Info(sb.ToString());
							sans.Add(entry);
						}
                        request.SANEntries = sans.ToArray();
                    }
                }
				List<Option> options = new List<Option>();
				if (request.SANEntries.Count() > 0)
				{
					var opt = new Option();
					opt.OptionName = "SAN";
					opt.OptionValue = "True";
					options.Add(opt);
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
					Options = options.ToArray()
				};
				if (!string.IsNullOrEmpty(BaseOption))
				{
					request.OrderRequestParameter.BaseOption = BaseOption;
				}
				return request;
            }
        }

    }
}
