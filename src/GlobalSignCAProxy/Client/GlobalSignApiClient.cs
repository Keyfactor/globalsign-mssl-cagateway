// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using CAProxy.AnyGateway.Models;
using CAProxy.Common;

using CSS.Common.Logging;
using CSS.PKI;
using CSS.PKI.X509;

using Keyfactor.Extensions.AnyGateway.GlobalSign.Api;
using Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Order;
using Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Query;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Keyfactor.Extensions.AnyGateway.GlobalSign.Client
{
	public class GlobalSignApiClient : LoggingClientBase
	{
		private readonly GlobalSignCAConfig Config;
		public GASService QueryService;
		public ManagedSSLService OrderService;

		public GlobalSignApiClient(GlobalSignCAConfig config)
		{
			Config = config;
			QueryService = new GASService() { Url = config.GetUrl(GlobalSignServiceType.QUERY) };
			OrderService = new ManagedSSLService() { Url = config.GetUrl(GlobalSignServiceType.ORDER) };
		}

		public List<OrderDetail> GetCertificatesForSync(bool fullSync, DateTime? lastSync)
		{
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			using (this.QueryService)
			{
				if (fullSync)
				{
					return GetCertificatesByDateRange(DateTime.MinValue, DateTime.UtcNow);
				}
				else //Incremental Sync
				{
					return GetCertificatesByDateRange(lastSync, DateTime.UtcNow);
				}
			}
		}

		private List<OrderDetail> GetCertificatesByDateRange(DateTime? fromDate, DateTime? toDate)
		{
			var tmpFromDate = fromDate ?? DateTime.MinValue;
			var tmpToDate = toDate ?? DateTime.UtcNow;

			QbV1GetOrderByDateRangeRequest req = new QbV1GetOrderByDateRangeRequest
			{
				QueryRequestHeader = new Services.Query.QueryRequestHeader
				{
					AuthToken = Config.GetQueryAuthToken()
				},
				FromDate = tmpFromDate.ToString(Constants.DATE_FORMAT_STRING, DateTimeFormatInfo.InvariantInfo),
				ToDate = tmpToDate.ToString(Constants.DATE_FORMAT_STRING, DateTimeFormatInfo.InvariantInfo),
				OrderQueryOption = new OrderQueryOption
				{
					ReturnOrderOption = "true",
					ReturnCertificateInfo = "true",
					ReturnFulfillment = "true",
					ReturnOriginalCSR = "true"
				}
			};
			Logger.Debug($"Retrieving all orders between {tmpFromDate} and {tmpToDate}");
			var allOrdersResponse = QueryService.GetOrderByDateRange(req);

			if (allOrdersResponse.QueryResponseHeader.SuccessCode == 0)
			{
				var retVal = allOrdersResponse.OrderDetails?.ToList() ?? new List<OrderDetail>();
				Logger.Debug($"Retrieved {retVal.Count} orders from GlobalSign");
				return retVal;
			}
			else
			{
				int errCode = int.Parse(allOrdersResponse.QueryResponseHeader.Errors[0].ErrorCode);
				Logger.Error($"Unable to retrieve certificates:");
				foreach (var e in allOrdersResponse.QueryResponseHeader.Errors)
				{
					Logger.Error($"{e.ErrorCode} | {e.ErrorField} | {e.ErrorMessage}");
				}
				var gsError = GlobalSignErrorIndex.GetGlobalSignError(errCode);
				Logger.Error(gsError.DetailedMessage);
				throw new UnsuccessfulRequestException(gsError.Message, gsError.HResult);
			}
		}

		public CAConnectorCertificate GetCertificateById(string caRequestID)
		{
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			QbV1GetOrderByOrderIdRequest request = new QbV1GetOrderByOrderIdRequest
			{
				QueryRequestHeader = new Services.Query.QueryRequestHeader
				{
					AuthToken = Config.GetQueryAuthToken()
				},
				OrderID = caRequestID,
				OrderQueryOption = new OrderQueryOption
				{
					// OrderStatus = "true",
					ReturnCertificateInfo = "true",
					ReturnOriginalCSR = "true",
					ReturnFulfillment = "true",
				}
			};

			using (var service = this.QueryService)
			{
				Logger.Debug($"Retrieving details of certificate with request ID {caRequestID}");
				var response = service.GetOrderByOrderID(request);
				if (response.OrderResponseHeader.SuccessCode == 0)
				{
					Logger.Debug($"Certificate with request ID {caRequestID} successfully retrieved");
					GlobalSignOrderStatus orderStatus = (GlobalSignOrderStatus)Enum.Parse(typeof(GlobalSignOrderStatus), response.OrderDetail.CertificateInfo.CertificateStatus);
					DateTime? subDate = DateTime.TryParse(response?.OrderDetail?.OrderInfo?.OrderDate, out DateTime orderDate) ? orderDate : (DateTime?)null;
					DateTime? resDate = DateTime.TryParse(response?.OrderDetail?.OrderInfo?.OrderCompleteDate, out DateTime completeDate) ? completeDate : (DateTime?)null;
					DateTime? revDate = DateTime.TryParse(response?.OrderDetail?.OrderInfo?.OrderDeactivatedDate, out DateTime deactivateDate) ? deactivateDate : (DateTime?)null;
					Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
					return new CAConnectorCertificate()
					{
						CARequestID = caRequestID,
						ProductID = response.OrderDetail?.OrderInfo?.ProductCode,
						SubmissionDate = subDate,
						ResolutionDate = resDate,
						Status = OrderStatus.ConvertToKeyfactorStatus(orderStatus),
						CSR = response.OrderDetail?.Fulfillment?.OriginalCSR,
						Certificate = response.OrderDetail?.Fulfillment?.ServerCertificate?.X509Cert,
						RevocationReason = 0,
						RevocationDate = orderStatus == GlobalSignOrderStatus.Revoked ? revDate : new DateTime?()
					};
				}
				else
				{
					int errCode = int.Parse(response.OrderResponseHeader.Errors[0].ErrorCode);
					var gsError = GlobalSignErrorIndex.GetGlobalSignError(errCode);
					Logger.Error(gsError.DetailedMessage);
					throw new UnsuccessfulRequestException(gsError.Message, gsError.HResult);
				}
			}
		}

		public CAConnectorCertificate PickupCertificateById(string caRequestId)
		{
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			Logger.Debug($"Attempting to pick up order with order ID {caRequestId}");
			QbV1GetOrderByOrderIdRequest request = new QbV1GetOrderByOrderIdRequest
			{
				QueryRequestHeader = new Services.Query.QueryRequestHeader
				{
					AuthToken = Config.GetQueryAuthToken()
				},
				OrderID = caRequestId,
				OrderQueryOption = new OrderQueryOption
				{
					ReturnCertificateInfo = "true",
					ReturnOriginalCSR = "true",
					ReturnFulfillment = "true"
				}
			};

			int retryCounter = 0;
			while (retryCounter <= Config.PickupRetries)
			{
				using (var service = this.QueryService)
				{
					var response = service.GetOrderByOrderID(request);

					if (response.OrderResponseHeader.SuccessCode == 0)
					{
						Logger.Debug($"Order with order ID {caRequestId} successfully picked up");
						GlobalSignOrderStatus orderStatus = (GlobalSignOrderStatus)Enum.Parse(typeof(GlobalSignOrderStatus), response.OrderDetail.CertificateInfo.CertificateStatus);
						if (orderStatus == GlobalSignOrderStatus.Issued)
						{
							DateTime? orderDate = DateTime.TryParse(response?.OrderDetail?.OrderInfo?.OrderDate, out DateTime orderDateTime) ? orderDateTime : (DateTime?)null;
							DateTime? completeDate = DateTime.TryParse(response?.OrderDetail?.OrderInfo?.OrderCompleteDate, out DateTime orderCompleteDate) ? orderCompleteDate : (DateTime?)null;
							DateTime? deactivateDate = DateTime.TryParse(response?.OrderDetail?.OrderInfo?.OrderDeactivatedDate, out DateTime orderDeactivateDate) ? orderDeactivateDate : (DateTime?)null;
							Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
							return new CAConnectorCertificate()
							{
								CARequestID = caRequestId,
								ProductID = response.OrderDetail.OrderInfo.ProductCode,
								SubmissionDate = orderDate,
								ResolutionDate = completeDate,
								Status = OrderStatus.ConvertToKeyfactorStatus(orderStatus),
								CSR = response.OrderDetail.Fulfillment.OriginalCSR,
								Certificate = response.OrderDetail.Fulfillment.ServerCertificate.X509Cert,
								RevocationReason = 0,
								RevocationDate = orderStatus == GlobalSignOrderStatus.Revoked ? deactivateDate : new DateTime?()
							};
						}
					}
					retryCounter++;
					string logMsg = $"Pickup certificate failed for order ID {caRequestId}. Attempt {retryCounter} of {Config.PickupRetries}.";
					if (retryCounter < Config.PickupRetries)
					{
						logMsg = logMsg + " Retrying...";
					}
					Logger.Debug(logMsg);
					Thread.Sleep(Config.PickupDelay * 1000);//convert seconds to ms for delay.
				}
			}

			var gsError = GlobalSignErrorIndex.GetGlobalSignError(-9916);
			string errorMsg = "Unable to pickup certificate during configured pickup window. Check for required approvals in GlobalSign portal. This can also be caused by a delay with GlobalSign, in which case the certificate will get picked up by a future sync";
			Logger.Error(errorMsg);
			Logger.Error(gsError.DetailedMessage);
			throw new UnsuccessfulRequestException(errorMsg, gsError.HResult);
		}

		public List<DomainDetail> GetDomains()
		{
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			var response = OrderService.GetDomains(new BmV1GetDomainsRequest { QueryRequestHeader = new Services.Order.QueryRequestHeader { AuthToken = Config.GetOrderAuthToken() } });
			if (response.QueryResponseHeader.SuccessCode == 0)
			{
				var retVal = response.DomainDetails?.ToList() ?? new List<DomainDetail>();
				Logger.Debug($"Successfully retrieved {retVal.Count} domains");
				return retVal;
			}

			int errCode = int.Parse(response.QueryResponseHeader.Errors[0].ErrorCode);
			var gsError = GlobalSignErrorIndex.GetGlobalSignError(errCode);
			Logger.Error(gsError.DetailedMessage);
			throw new UnsuccessfulRequestException(gsError.Message, gsError.HResult);
		}

		public List<SearchMsslProfileDetail> GetProfiles()
		{
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			var response = OrderService.GetMSSLProfiles(new BmV1GetMsslProfilesRequest { QueryRequestHeader = new Services.Order.QueryRequestHeader { AuthToken = Config.GetOrderAuthToken() } });
			if (response.QueryResponseHeader.SuccessCode == 0)
			{
				var retVal = response.SearchMSSLProfileDetails.ToList();
				Logger.Debug($"Successfully retrieved {retVal.Count} profiles");
				return retVal;
			}
			int errCode = int.Parse(response.QueryResponseHeader.Errors[0].ErrorCode);
			var gsError = GlobalSignErrorIndex.GetGlobalSignError(errCode);
			Logger.Error(gsError.DetailedMessage);
			throw new UnsuccessfulRequestException(gsError.Message, gsError.HResult);
		}

		public EnrollmentResult Enroll(GlobalSignEnrollRequest enrollRequest)
		{
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			using (this.OrderService)
			{
				var rawRequest = enrollRequest.Request;
				//Logger.Trace($"Request details:");
				//Logger.Trace($"Profile ID: {rawRequest.MSSLProfileID}");
				//Logger.Trace($"Domain ID: {rawRequest.MSSLDomainID}");
				//Logger.Trace($"Contact Info: {rawRequest.ContactInfo.FirstName}, {rawRequest.ContactInfo.LastName}, {rawRequest.ContactInfo.Email}, {rawRequest.ContactInfo.Phone}");
				//Logger.Trace($"SAN Count: {rawRequest.SANEntries.Count()}");
				//if (rawRequest.SANEntries.Count() > 0)
				//{
				//	Logger.Trace($"SANs: {string.Join(",", rawRequest.SANEntries.Select(s => s.SubjectAltName))}");
				//}
				//Logger.Trace($"Product Code: {rawRequest.OrderRequestParameter.ProductCode}");
				//Logger.Trace($"Order Kind: {rawRequest.OrderRequestParameter.OrderKind}");


				Logger.Trace($"BmV2PvOrderRequest details:");
				Logger.Trace($"PvOrderRequest.OrderRequestHeader.AuthToken.Username: {rawRequest.OrderRequestHeader.AuthToken.UserName ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestHeader.AuthToken.Password: <Redacted>");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.ProductCode: {rawRequest.OrderRequestParameter.ProductCode ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.BaseOption: {rawRequest.OrderRequestParameter.BaseOption ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.OrderKind: {rawRequest.OrderRequestParameter.OrderKind ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.Licenses: {rawRequest.OrderRequestParameter.Licenses ?? string.Empty}");
				foreach (var opt in rawRequest.OrderRequestParameter.Options)
				{
					Logger.Trace($"PvOrderRequest.OrderRequestParameter.Option[{opt.OptionName}]: {opt.OptionValue}");
				}
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.ValidityPeriod.Months: {rawRequest.OrderRequestParameter.ValidityPeriod.Months ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.ValidityPeriod.NotBefore: {rawRequest.OrderRequestParameter.ValidityPeriod.NotBefore ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.ValidityPeriod.NotAfter: {rawRequest.OrderRequestParameter.ValidityPeriod.NotAfter ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.CSR: {rawRequest.OrderRequestParameter.CSR ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.RenewalTargetOrderID: {rawRequest.OrderRequestParameter.RenewalTargetOrderID ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.TargetCert: {rawRequest.OrderRequestParameter.TargetCERT ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.SpecialInstructions: {rawRequest.OrderRequestParameter.SpecialInstructions ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.Coupon: {rawRequest.OrderRequestParameter.Coupon ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.OrderRequestParameter.Campaign: {rawRequest.OrderRequestParameter.Campaign ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.MsslProfileId: {rawRequest.MSSLProfileID ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.MsslDomainId: {rawRequest.MSSLDomainID ?? string.Empty}");
				Logger.Trace($"PvOrderRequest.SubId: {rawRequest.SubID ?? string.Empty}");
				if (rawRequest.PVSealInfo != null)
				{
					Logger.Trace($"PvOrderRequest.PvSealInfo.AddressLine1: {rawRequest.PVSealInfo.AddressLine1 ?? string.Empty}");
					Logger.Trace($"PvOrderRequest.PvSealInfo.AddressLine2: {rawRequest.PVSealInfo.AddressLine2 ?? string.Empty}");
					Logger.Trace($"PvOrderRequest.PvSealInfo.AddressLine3: {rawRequest.PVSealInfo.AddressLine3 ?? string.Empty}");
					Logger.Trace($"PvOrderRequest.PvSealInfo.PostalCode: {rawRequest.PVSealInfo.PostalCode ?? string.Empty}");
					Logger.Trace($"PvOrderRequest.PvSealInfo.Phone: {rawRequest.PVSealInfo.Phone ?? string.Empty}");
					Logger.Trace($"PvOrderRequest.PvSealInfo.Fax: {rawRequest.PVSealInfo.Fax ?? string.Empty}");
				}
				if (rawRequest.ContactInfo != null)
				{
					Logger.Trace($"PvOrderRequest.ContactInfo.FirstName: {rawRequest.ContactInfo.FirstName ?? string.Empty}");
					Logger.Trace($"PvOrderRequest.ContactInfo.LastName: {rawRequest.ContactInfo.LastName ?? string.Empty}");
					Logger.Trace($"PvOrderRequest.ContactInfo.Phone: {rawRequest.ContactInfo.Phone ?? string.Empty}");
					Logger.Trace($"PvOrderRequest.ContactInfo.Email: {rawRequest.ContactInfo.Email ?? string.Empty}");
				}
				foreach (var san in rawRequest.SANEntries)
				{
					Logger.Trace($"PvOrderRequest.SAN: {san.SubjectAltName}, {san.SANOptionType}");
				}
				foreach (var ext in rawRequest.Extensions)
				{
					Logger.Trace($"PvOrderRequest.Extensions[{ext.Name}]: {ext.Value}");
				}

				var response = OrderService.PVOrder(enrollRequest.Request);
				if (response.OrderResponseHeader.SuccessCode == 0)
				{
					Logger.Debug($"Enrollment request successfully submitted");
					var certStatus = (GlobalSignOrderStatus)Enum.Parse(typeof(GlobalSignOrderStatus), response.PVOrderDetail.CertificateInfo.CertificateStatus);

					switch (certStatus)
					{
						case GlobalSignOrderStatus.Issued:
							return new EnrollmentResult
							{
								CARequestID = response.OrderID,
								Certificate = response.PVOrderDetail.Fulfillment.ServerCertificate.X509Cert,
								Status = (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.ISSUED,
							};

						case GlobalSignOrderStatus.PendingApproval:
						case GlobalSignOrderStatus.Waiting:
							return new EnrollmentResult
							{
								CARequestID = response.OrderID,
								Status = (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.EXTERNAL_VALIDATION,
								StatusMessage = $"Enrollment is pending review.  Check GlobalSign Portal for more detail."
							};
					}
				}

				int errorCode = int.Parse(response.OrderResponseHeader.Errors[0].ErrorCode);
				GlobalSignError err = GlobalSignErrorIndex.GetGlobalSignError(errorCode);
				if (errorCode <= -101 && errorCode >= -104) // Invalid parameter errors, provide more information
				{
					err.ErrorDetails = string.Format(err.ErrorDetails, response.OrderResponseHeader.Errors[0].ErrorField);
				}
				foreach (var e in response.OrderResponseHeader.Errors)
				{
					Logger.Error($"{e.ErrorCode}|{e.ErrorField}|{e.ErrorMessage}");
				}
				return new EnrollmentResult
				{
					Status = (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.FAILED,
					StatusMessage = $"Enrollment failed. {err.DetailedMessage}"
				};
			}
		}

		public EnrollmentResult Renew(GlobalSignRenewRequest renewRequest)
		{
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			using (this.OrderService)
			{
				var response = OrderService.PVOrder(renewRequest.Request);
				if (response.OrderResponseHeader.SuccessCode == 0)
				{
					Logger.Debug($"Renewal request successfully submitted");
					var certStatus = (GlobalSignOrderStatus)Enum.Parse(typeof(GlobalSignOrderStatus), response.PVOrderDetail.CertificateInfo.CertificateStatus);

					switch (certStatus)
					{
						case GlobalSignOrderStatus.Issued:
							return new EnrollmentResult
							{
								CARequestID = response.OrderID,
								Certificate = response.PVOrderDetail.Fulfillment.ServerCertificate.X509Cert,
								Status = (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.ISSUED,
							};

						case GlobalSignOrderStatus.PendingApproval:
						case GlobalSignOrderStatus.Waiting:
							return new EnrollmentResult
							{
								CARequestID = response.OrderID,
								Status = (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.EXTERNAL_VALIDATION,
								StatusMessage = $"Enrollment is pending review.  Check GlobalSign Portal for more detail."
							};
					}
				}
				GlobalSignError err = GlobalSignErrorIndex.GetGlobalSignError(int.Parse(response.OrderResponseHeader.Errors[0].ErrorCode));
				foreach (var e in response.OrderResponseHeader.Errors)
				{
					Logger.Error($"{e.ErrorCode}|{e.ErrorField}|{e.ErrorMessage}");
				}
				return new EnrollmentResult
				{
					Status = (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.FAILED,
					StatusMessage = $"Enrollment failed. {err.DetailedMessage}"
				};
			}
		}

		public EnrollmentResult Reissue(GlobalSignReissueRequest reissueRequest, string priorSn)
		{
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			using (this.QueryService)
			{
				var response = QueryService.ReIssue(reissueRequest.Request);
				if (response.OrderResponseHeader.SuccessCode == 0)
				{
					Logger.Debug($"Reissue request successfully submitted");
					var pickupResponse = PickupCertificateById(response.OrderID);
					var cert = CertificateConverterFactory.FromPEM(pickupResponse.Certificate).ToX509Certificate2();

					if (pickupResponse.Status == 20 || (cert.SerialNumber != priorSn))
					{
						return new EnrollmentResult
						{
							CARequestID = response.OrderID,
							Status = (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.ISSUED,
							Certificate = pickupResponse.Certificate
						};
					}
				}

				GlobalSignError err = GlobalSignErrorIndex.GetGlobalSignError(int.Parse(response.OrderResponseHeader.Errors[0].ErrorCode));
				foreach (var e in response.OrderResponseHeader.Errors)
				{
					Logger.Error($"{e.ErrorCode}|{e.ErrorField}|{e.ErrorMessage}");
				}
				return new EnrollmentResult
				{
					Status = (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.FAILED,
					StatusMessage = $"Enrollment failed. {err.DetailedMessage}"
				};
			}
		}

		public int RevokeCertificateById(string caRequestId)
		{
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			using (this.OrderService)
			{
				BmV1ModifyMsslOrderRequest request = new BmV1ModifyMsslOrderRequest
				{
					OrderRequestHeader = new Services.Order.OrderRequestHeader { AuthToken = Config.GetOrderAuthToken() },
					OrderID = caRequestId,
					ModifyOrderOperation = "Revoke"
				};
				Logger.Debug($"Attempting to revoke certificate with request ID {caRequestId}");
				var response = OrderService.ModifyMSSLOrder(request);
				if (response.OrderResponseHeader.SuccessCode == 0)
				{
					Logger.Debug($"Certificate with request ID {caRequestId} successfully revoked");
					return (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.REVOKED;
				}

				int errCode = int.Parse(response.OrderResponseHeader.Errors[0].ErrorCode);
				foreach (var e in response.OrderResponseHeader.Errors)
				{
					Logger.Error($"{e.ErrorCode}|{e.ErrorField}|{e.ErrorMessage}");
				}
				var gsError = GlobalSignErrorIndex.GetGlobalSignError(errCode);
				Logger.Error(gsError.DetailedMessage);
				throw new UnsuccessfulRequestException(gsError.Message, gsError.HResult);
			}
		}
	}
}