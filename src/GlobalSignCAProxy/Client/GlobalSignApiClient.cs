﻿// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using CAProxy.AnyGateway.Models;
using CAProxy.Common;
using CSS.Common.Logging;
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

            var allOrdersResponse = QueryService.GetOrderByDateRange(req);

            if (allOrdersResponse.QueryResponseHeader.SuccessCode == 0)
            {
                return allOrdersResponse.OrderDetails?.ToList() ?? new List<OrderDetail>();
            }
            else
            {
                int errCode = int.Parse(allOrdersResponse.QueryResponseHeader.Errors[0].ErrorCode);
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
                var response = service.GetOrderByOrderID(request);
                if (response.OrderResponseHeader.SuccessCode == 0)
                {
                    GlobalSignOrderStatus orderStatus = (GlobalSignOrderStatus)Enum.Parse(typeof(GlobalSignOrderStatus), response.OrderDetail.CertificateInfo.CertificateStatus);

                    Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                    return new CAConnectorCertificate()
                    {
                        CARequestID = caRequestID,
                        ProductID = response.OrderDetail?.OrderInfo?.ProductCode,
                        SubmissionDate = DateTime.Parse(response.OrderDetail?.OrderInfo?.OrderDate),
                        ResolutionDate = DateTime.Parse(response.OrderDetail?.OrderInfo?.OrderCompleteDate),
                        Status = (int)orderStatus,
                        CSR = response.OrderDetail?.Fulfillment?.OriginalCSR,
                        Certificate = response.OrderDetail?.Fulfillment?.ServerCertificate?.X509Cert,
                        RevocationReason = 0,
                        RevocationDate = orderStatus == GlobalSignOrderStatus.Revoked ? DateTime.Parse(response.OrderDetail?.OrderInfo?.OrderDeactivatedDate) : new DateTime?()
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
            QbV1GetOrderByOrderIdRequest request = new QbV1GetOrderByOrderIdRequest
            {
                OrderID = caRequestId,
                OrderQueryOption = new OrderQueryOption
                {
                    ReturnCertificateInfo = "true",
                    ReturnOriginalCSR = "true"
                }
            };

            int retryCounter = 0;
            while (retryCounter < Config.PickupRetries)
            {
                using (var service = this.QueryService)
                {
                    var response = service.GetOrderByOrderID(request);

                    if (response.OrderResponseHeader.SuccessCode == 0)
                    {
                        GlobalSignOrderStatus orderStatus = (GlobalSignOrderStatus)Enum.Parse(typeof(GlobalSignOrderStatus), response.OrderDetail.CertificateInfo.CertificateStatus);

                        Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                        return new CAConnectorCertificate()
                        {
                            CARequestID = caRequestId,
                            ProductID = response.OrderDetail.OrderInfo.ProductCode,
                            SubmissionDate = DateTime.Parse(response.OrderDetail.OrderInfo.OrderDate),
                            ResolutionDate = DateTime.Parse(response.OrderDetail.OrderInfo.OrderCompleteDate),
                            Status = (int)orderStatus,
                            CSR = response.OrderDetail.Fulfillment.OriginalCSR,
                            Certificate = response.OrderDetail.Fulfillment.ServerCertificate.X509Cert,
                            RevocationReason = 0,
                            RevocationDate = orderStatus == GlobalSignOrderStatus.Revoked ? DateTime.Parse(response.OrderDetail.OrderInfo.OrderDeactivatedDate) : new DateTime?()
                        };
                    }

                    Thread.Sleep(Config.PickupDelay * 1000);//convert seconds to ms for delay. 
                    retryCounter++;
                }

            }

            var gsError = GlobalSignErrorIndex.GetGlobalSignError(-9916);
            Logger.Error("Unable to pickup certificate during configured pickup window. Check for required approvals in GlobalSign portal");
            Logger.Error(gsError.DetailedMessage);
            throw new UnsuccessfulRequestException(gsError.Message, gsError.HResult);
        }
        public List<DomainDetail> GetDomains()
        {
            var response = OrderService.GetDomains(new BmV1GetDomainsRequest { QueryRequestHeader = new Services.Order.QueryRequestHeader { AuthToken = Config.GetOrderAuthToken() } });
            if (response.QueryResponseHeader.SuccessCode == 0)
            {
                return response.DomainDetails?.ToList() ?? new List<DomainDetail>();
            }

            int errCode = int.Parse(response.QueryResponseHeader.Errors[0].ErrorCode);
            var gsError = GlobalSignErrorIndex.GetGlobalSignError(errCode);
            Logger.Error(gsError.DetailedMessage);
            throw new UnsuccessfulRequestException(gsError.Message, gsError.HResult);
        }
        public List<SearchMsslProfileDetail> GetProfiles()
        {
            var response = OrderService.GetMSSLProfiles(new BmV1GetMsslProfilesRequest { QueryRequestHeader = new Services.Order.QueryRequestHeader { AuthToken = Config.GetOrderAuthToken() } });
            if (response.QueryResponseHeader.SuccessCode == 0)
            {
                return response.SearchMSSLProfileDetails.ToList();
            }
            int errCode = int.Parse(response.QueryResponseHeader.Errors[0].ErrorCode);
            var gsError = GlobalSignErrorIndex.GetGlobalSignError(errCode);
            Logger.Error(gsError.DetailedMessage);
            throw new UnsuccessfulRequestException(gsError.Message, gsError.HResult);
        }
        public EnrollmentResult Enroll(GlobalSignEnrollRequest enrollRequest)
        {
            using (this.OrderService)
            {
                var response = OrderService.PVOrder(enrollRequest.Request);
                if (response.OrderResponseHeader.SuccessCode == 0)
                {
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
            using (this.OrderService)
            {
                var response = OrderService.PVOrder(renewRequest.Request);
                if (response.OrderResponseHeader.SuccessCode == 0)
                {
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
            using (this.QueryService)
            {
                var response = QueryService.ReIssue(reissueRequest.Request);
                if (response.OrderResponseHeader.SuccessCode == 0)
                {
                    var pickupResponse = PickupCertificateById(response.OrderID);
                    X509Certificate2 cert = new X509Certificate2(Convert.FromBase64String(pickupResponse.Certificate));

                    if (pickupResponse.Status == 4 || (cert.SerialNumber != priorSn))
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
            using (this.OrderService)
            {
                BmV1ModifyMsslOrderRequest request = new BmV1ModifyMsslOrderRequest
                {
                    OrderRequestHeader = new Services.Order.OrderRequestHeader { AuthToken = Config.GetOrderAuthToken() },
                    OrderID = caRequestId,
                    ModifyOrderOperation = "Revoke"
                };

                var response = OrderService.ModifyMSSLOrder(request);
                if (response.OrderResponseHeader.SuccessCode == 0)
                {
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
