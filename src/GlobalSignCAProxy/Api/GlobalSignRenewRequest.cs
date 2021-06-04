using Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Order;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                BmV2PvOrderRequest request = new BmV2PvOrderRequest();
                request.OrderRequestHeader = new OrderRequestHeader { AuthToken = Config.GetOrderAuthToken()};
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
                    RenewalTargetOrderID = RenewalTargetOrderId,
                    ValidityPeriod = validityPeriod,
                };
                return request;
            }
        }

    }
}
