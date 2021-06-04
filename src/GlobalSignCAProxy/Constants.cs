using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.AnyGateway.GlobalSign
{
    class Constants
    {
        public const string ORDER_TEST_URL = "https://test-gcc.globalsign.com/kb/ws/v2/ManagedSSLService";
        public const string ORDER_PROD_URL = "https://system.globalsign.com/kb/ws/v2/ManagedSSLService";
        public const string QUERY_TEST_URL = "https://test-gcc.globalsign.com/kb/ws/v1/GASService";
        public const string QUERY_PROD_URL = "https://system.globalsign.com/kb/ws/v1/GASService";
        public const string DATE_FORMAT_STRING = "yyyy-MM-ddTHH:mm:ss.fffZ";
    }
}
