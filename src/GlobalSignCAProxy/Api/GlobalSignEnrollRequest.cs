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
	public class GlobalSignEnrollRequest
	{
		internal GlobalSignCAConfig Config;

		public GlobalSignEnrollRequest(GlobalSignCAConfig config)
		{
			Config = config;
		}

		public string CSR { get; set; }
		public string ProductCode { get; set; }
		public string CommonName { get; set; }

		public string BaseOption
		{
			get
			{
				if (!string.IsNullOrEmpty(CommonName))
				{
					if (CommonName.StartsWith("*"))
					{
						return "wildcard";
					}
					else
					{
						return null;
					}
				}
				else
				{
					return null;
				}
			}
		}

		public string OrderKind { get; set; }
		public string Licenses { get; set; }
		public string Months { get; set; }
		public string MsslProfileId { get; set; }
		public string MsslDomainId { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Phone { get; set; }
		public string Email { get; set; }
		public List<string> SANs { get; set; }
		public PvSealInfo Seal { get; set; }
		public MsslEvProfileInfo EVProfile { get; set; }

		public BmV2PvOrderRequest Request
		{
			get
			{
				BmV2PvOrderRequest request = new BmV2PvOrderRequest();
				request.OrderRequestHeader = new OrderRequestHeader { AuthToken = Config.GetOrderAuthToken() };
				request.MSSLProfileID = MsslProfileId;
				request.MSSLDomainID = MsslDomainId;
				request.ContactInfo = new ContactInfo
				{
					FirstName = FirstName,
					LastName = LastName,
					Phone = Phone,
					Email = Email
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
								entry.SANOptionType = "13";
							}
							else
							{
								entry.SANOptionType = "7";
							}
							sans.Add(entry);
						}
						request.SANEntries = sans.ToArray();
					}
				}
				ValidityPeriod validityPeriod = new ValidityPeriod();
				validityPeriod.Months = Months;
				request.OrderRequestParameter = new OrderRequestParameter
				{
					ProductCode = ProductCode,
					OrderKind = OrderKind,
					Licenses = Licenses,
					CSR = CSR,
					ValidityPeriod = validityPeriod
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