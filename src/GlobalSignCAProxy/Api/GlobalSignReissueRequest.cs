using Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
