﻿// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using CAProxy.AnyGateway;
using CAProxy.AnyGateway.Interfaces;
using CAProxy.AnyGateway.Models;
using CAProxy.Common;
using CAProxy.Common.Config;
using CSS.Common.Logging;
using CSS.PKI;
using Keyfactor.Extensions.AnyGateway.GlobalSign.Api;
using Keyfactor.Extensions.AnyGateway.GlobalSign.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;


namespace Keyfactor.Extensions.AnyGateway.GlobalSign
{
    public class GlobalSignCAProxy : BaseCAConnector
    {
        private GlobalSignCAConfig Config { get; set; }
        public override void Initialize(ICAConnectorConfigProvider configProvider)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            string rawConfig = JsonConvert.SerializeObject(configProvider.CAConnectionData);
            Config = JsonConvert.DeserializeObject<GlobalSignCAConfig>(rawConfig);
            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
        }

        public override EnrollmentResult Enroll(ICertificateDataReader certificateDataReader, string csr, string subject, Dictionary<string, string[]> san, EnrollmentProductInfo productInfo, PKIConstants.X509.RequestFormat requestFormat, RequestUtilities.EnrollmentType enrollmentType)
        {
            CAProxy.Common.Config.ADUserInfoResolver userInfoResolver = new ADUserInfoResolver();
            
            var requestor = productInfo.ProductParameters["Keyfactor-Requester"];
            var userInfo = userInfoResolver.Resolve(requestor);

            try    
            {
                GlobalSignApiClient apiClient = new GlobalSignApiClient(Config);

                string priorSn = string.Empty;
                if (productInfo.ProductParameters.ContainsKey("priorcertsn"))
                {
                    priorSn = productInfo.ProductParameters["priorcertsn"];
                }
                //get domain ID for enrollment
                var commonName = ParseSubject(subject, "CN=");
                var months = productInfo.ProductParameters["Lifetime"];

                //get domain
                var domain = apiClient.GetDomains().Where(d => commonName.EndsWith(d.DomainName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                
                var productType = GlobalSignCertType.AllTypes.Where(x => x.ProductCode.Equals(productInfo.ProductID, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                
                CAConnectorCertificate priorCert = null;
                switch (enrollmentType)
                {
                    case RequestUtilities.EnrollmentType.New:
                        
                        GlobalSignEnrollRequest request = new GlobalSignEnrollRequest(Config)
                        {
                            MsslDomainId = domain?.DomainID,
                            MsslProfileId = domain?.MSSLProfileID,
                            CSR = csr,
                            Licenses = "1",
                            OrderKind = "new",
                            Months = months,
                            FirstName = userInfo.Name,
                            LastName = userInfo.Name,
                            Email = domain?.ContactInfo?.Email,
                            Phone = domain?.ContactInfo?.Phone,
                            CommonName = commonName,
                            ProductCode = productType.ProductCode,
                        };

                        return apiClient.Enroll(request);

                    case RequestUtilities.EnrollmentType.Renew:
                        priorCert = certificateDataReader.GetCertificateRecord(CSS.Common.DataConversion.HexToBytes(priorSn));

                        GlobalSignRenewRequest renewRequest = new GlobalSignRenewRequest(Config)
                        {
                            MsslDomainId = domain?.DomainID,
                            MsslProfileId = domain?.MSSLProfileID,
                            CSR = csr,
                            Licenses = "1",
                            OrderKind = "renewal",
                            Months = months,
                            FirstName = userInfo.Name,
                            LastName = userInfo.Name,
                            Email = domain?.ContactInfo?.Email,
                            Phone = domain?.ContactInfo?.Phone,
                            CommonName = commonName,
                            ProductCode = productType.ProductCode,
                            RenewalTargetOrderId = priorCert.CARequestID
                        };

                        return apiClient.Renew(renewRequest);
                    case RequestUtilities.EnrollmentType.Reissue:
                        priorCert = certificateDataReader.GetCertificateRecord(CSS.Common.DataConversion.HexToBytes(priorSn));

                        GlobalSignReissueRequest reissueRequest = new GlobalSignReissueRequest(Config) 
                        { 
                            CSR=csr,
                            OrderID = priorCert.CARequestID
                        };

                        return apiClient.Reissue(reissueRequest, priorSn);
                    default:
                        return new EnrollmentResult { Status = 30, StatusMessage = $"Unsupported enrollment type {enrollmentType}" };
                }
            }
            catch (UnsuccessfulRequestException uEx)
            {
                Logger.Error($"Error enrolling for certificate with subject {subject}");
                Logger.Error(uEx);
                return new EnrollmentResult
                {
                    StatusMessage = $"{uEx.Message}",
                    Status = (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.FAILED
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Unhandled exception enrolling for certificate with subject {subject}");
                Logger.Error(ex);
                return new EnrollmentResult
                {
                    StatusMessage = $"{ex.Message}",
                    Status = (int)CSS.PKI.PKIConstants.Microsoft.RequestDisposition.FAILED
                };
            }
        }

        public override void Synchronize(ICertificateDataReader certificateDataReader, BlockingCollection<CAConnectorCertificate> blockingBuffer, CertificateAuthoritySyncInfo certificateAuthoritySyncInfo, CancellationToken cancelToken)
        {
            try
            {
                GlobalSignApiClient apiClient = new GlobalSignApiClient(Config);

                DateTime? syncFrom = certificateAuthoritySyncInfo.DoFullSync ? new DateTime(2000,01,01) : certificateAuthoritySyncInfo.OverallLastSync;
                var certs = apiClient.GetCertificatesForSync(certificateAuthoritySyncInfo.DoFullSync, syncFrom);

                foreach (var c in certs)
                {
                    GlobalSignOrderStatus orderStatus = (GlobalSignOrderStatus)Enum.Parse(typeof(GlobalSignOrderStatus), c.CertificateInfo.CertificateStatus);
                    var certToAdd = new CAConnectorCertificate()
                    {
                        CARequestID = c.OrderInfo?.OrderId,
                        ProductID = c.OrderInfo?.ProductCode,
                        SubmissionDate = DateTime.Parse(c.OrderInfo?.OrderDate),
                        ResolutionDate = DateTime.Parse(c.OrderInfo?.OrderCompleteDate),
                        Status = (int)orderStatus,
                        CSR = c.Fulfillment?.OriginalCSR,
                        Certificate = c.Fulfillment?.ServerCertificate?.X509Cert,
                        RevocationReason = 0,
                        RevocationDate = orderStatus == GlobalSignOrderStatus.Revoked ? DateTime.Parse(c.OrderInfo?.OrderDeactivatedDate) : new DateTime?()
                    };

                    blockingBuffer.Add(certToAdd);
                }

                blockingBuffer.CompleteAdding();
            }
            catch (UnsuccessfulRequestException uEx)
            {
                Logger.Error("Error requesting certificates for sync. Stopping sync process");
                Logger.Error(uEx);
                blockingBuffer.CompleteAdding();
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception during sync. Stopping sync process");
                Logger.Error(ex);
                blockingBuffer.CompleteAdding();
            }

        }

        public override int Revoke(string caRequestID, string hexSerialNumber, uint revocationReason)
        {
            try
            {
                GlobalSignApiClient apiClient = new GlobalSignApiClient(Config);
                return apiClient.RevokeCertificateById(caRequestID);
            }
            catch (UnsuccessfulRequestException uEx)
            {
                Logger.Error($"Error revoking certificate with request id {caRequestID}");
                Logger.Error(uEx);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Unhandled exception revoking certificate with request id {caRequestID}");
                Logger.Error(ex);
                throw;
            }
        }

        public override CAConnectorCertificate GetSingleRecord(string caRequestID)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            try
            {
                GlobalSignApiClient apiClient = new GlobalSignApiClient(Config);
                return apiClient.GetCertificateById(caRequestID);
            }
            catch (UnsuccessfulRequestException uEx)
            {
                Logger.Error($"Error requesting certificate detail for caRequestID: {caRequestID}");
                Logger.Error(uEx);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Unhandled Exception requesting certificate detail for caRequestID: {caRequestID}");
                Logger.Error(ex);
                throw;
            }
        }

        public override void Ping()
        {
            Logger.Info("Ping reqeuest recieved");
        }

        public override void ValidateCAConnectionInfo(Dictionary<string, object> connectionInfo)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            string rawConfig = JsonConvert.SerializeObject(connectionInfo);
            GlobalSignCAConfig validateConfig = JsonConvert.DeserializeObject<GlobalSignCAConfig>(rawConfig);

            var apiClient = new GlobalSignApiClient(validateConfig);
            apiClient.GetDomains().ForEach(x => Logger.Info($"Connection established for {x.DomainName}"));
            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);

        }

        public override void ValidateProductInfo(EnrollmentProductInfo productInfo, Dictionary<string, object> connectionInfo)
        {
            var certType = GlobalSignCertType.AllTypes.Find(x => x.ProductCode.Equals(productInfo.ProductID, StringComparison.InvariantCultureIgnoreCase));

            if (certType == null)
            {
                throw new ArgumentException($"Cannot find {productInfo.ProductID}", "ProductId");
            }

            Logger.Info($"Validated {certType.DisplayName} ({certType.ProductCode})configured for AnyGateway");
        }

        #region Obsolete Methods
        [Obsolete]
        public override EnrollmentResult Enroll(string csr, string subject, Dictionary<string, string[]> san, EnrollmentProductInfo productInfo, CSS.PKI.PKIConstants.X509.RequestFormat requestFormat, RequestUtilities.EnrollmentType enrollmentType)
        {
            return UnsupportedMethod();
        }
        [Obsolete]
        public override void Synchronize(ICertificateDataReader certificateDataReader, BlockingCollection<CertificateRecord> blockingBuffer, CertificateAuthoritySyncInfo certificateAuthoritySyncInfo, CancellationToken cancelToken, string logicalName)
        {
            UnsupportedMethod();
        }
        #endregion

        #region Private Methods
        private EnrollmentResult UnsupportedMethod()
        {
            Logger.Error("This AnyGateway plugin is supported on AnyGateway 20.9+");
            throw new NotImplementedException("This AnyGateway plugin is supported on AnyGateway 20.9+");
        }
        private static string ParseSubject(string subject, string rdn)
        {
            string escapedSubject = subject.Replace("\\,", "|");
            string rdnString = escapedSubject.Split(',').ToList().Where(x => x.Contains(rdn)).FirstOrDefault();

            if (!string.IsNullOrEmpty(rdnString))
            {
                return rdnString.Replace(rdn, "").Replace("|", ",").Trim();
            }
            else
            {

                throw new Exception($"The request is missing a {rdn} value");
            }
        }

        #endregion
    }
}

