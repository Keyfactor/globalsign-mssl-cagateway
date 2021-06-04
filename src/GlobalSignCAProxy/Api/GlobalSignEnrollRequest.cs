using Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Order;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
