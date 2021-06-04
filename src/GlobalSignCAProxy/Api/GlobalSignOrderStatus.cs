
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.AnyGateway.GlobalSign.Api
{
    public enum GlobalSignOrderStatus
    {
        Initial = 1,
        Waiting = 2,
        Canceled = 3,
        Issued = 4,
        Cancelled = 5,
        Revoking = 6,
        Revoked = 7,
        PendingApproval = 8,
        Locked = 9,
        Denied = 10
    }
}
