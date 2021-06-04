// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

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
