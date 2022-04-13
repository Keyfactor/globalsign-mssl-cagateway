// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using CSS.PKI;

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

	public static class OrderStatus
	{
		public static int ConvertToKeyfactorStatus(GlobalSignOrderStatus status)
		{
			switch (status)
			{
				case GlobalSignOrderStatus.Issued:
					return (int)PKIConstants.Microsoft.RequestDisposition.ISSUED;

				case GlobalSignOrderStatus.Revoked:
				case GlobalSignOrderStatus.Revoking:
					return (int)PKIConstants.Microsoft.RequestDisposition.REVOKED;

				case GlobalSignOrderStatus.PendingApproval:
				case GlobalSignOrderStatus.Waiting:
					return (int)PKIConstants.Microsoft.RequestDisposition.EXTERNAL_VALIDATION;

				case GlobalSignOrderStatus.Initial:
					return (int)PKIConstants.Microsoft.RequestDisposition.IN_PROCESS;

				case GlobalSignOrderStatus.Denied:
					return (int)PKIConstants.Microsoft.RequestDisposition.DENIED;

				case GlobalSignOrderStatus.Canceled:
				case GlobalSignOrderStatus.Cancelled:
					return (int)PKIConstants.Microsoft.RequestDisposition.FAILED;

				default:
					return (int)PKIConstants.Microsoft.RequestDisposition.UNKNOWN;
			}
		}
	}
}