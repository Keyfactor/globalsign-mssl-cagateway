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
using Keyfactor.Extensions.AnyGateway.GlobalSign.Services.Order;

using Newtonsoft.Json;

using Org.BouncyCastle.Crypto.Tls;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Web.Services.Configuration;

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
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			string requesterName = "";
			if (productInfo.ProductParameters.ContainsKey("ContactName") && !string.IsNullOrEmpty(productInfo.ProductParameters["ContactName"]))
			{
				requesterName = productInfo.ProductParameters["ContactName"];
			}

			if (string.IsNullOrEmpty(requesterName))
			{
				if (productInfo.ProductParameters.ContainsKey("Keyfactor-Requester"))
				{
					var requestor = productInfo.ProductParameters["Keyfactor-Requester"];
					if (!string.IsNullOrEmpty(requestor))
					{
						try
						{
							ADUserInfoResolver userInfoResolver = new ADUserInfoResolver();
							Logger.Debug($"Resolving requesting user as '{requestor}'");
							var userInfo = userInfoResolver.Resolve(requestor);
							requesterName = userInfo.Name;
						} catch (Exception) { }
					}
				}
			}

			if (string.IsNullOrEmpty(requesterName))
			{
				throw new Exception("ContactName configuration field is required but not found, or could not be looked up");
			}

			try
			{
				GlobalSignApiClient apiClient = new GlobalSignApiClient(Config);
				Logger.Debug("Parsing enrollment values:");
				string priorSn = string.Empty;
				if (productInfo.ProductParameters.ContainsKey("priorcertsn"))
				{
					priorSn = productInfo.ProductParameters["priorcertsn"];
					Logger.Debug($"Prior cert sn: {priorSn}");
				}
				//get domain ID for enrollment
				// First, determine if there is a CN in the subject and, if so, find a domain that matches the end of the CN
				// If no CN is found, go through the DNS Name SANs in order, and find a domain that maches the end of one of those SANs
				// If a match is found, set the common name to that SAN (GlobalSign API requires the CommonName field be populated)
				string commonName = null;
				DomainDetail domain = null;
				var allDomains = apiClient.GetDomains();
				// Only acccept domains that are able to issue certificates
				// Status 3 - Approved, can issue certificates
				// Status 7/9/0 - Domain Renewal is in process, can issue certificates until expiration date
				List<string> validDomainStatus = new List<string>() { "3", "7", "9", "10" };
				List<DomainDetail> validDomains = allDomains.Where(d => validDomainStatus.Contains(d.DomainStatus)).ToList();
				if (validDomains.Count() == 0)
				{
					throw new Exception("No domains found that are valid for certificate enrollment");
				}
				try
				{
					commonName = ParseSubject(subject, "CN=");
				}
				catch
				{
					Logger.Warn("Subject is missing a CN value. Using SAN domain lookup instead");
				}
				StringBuilder rawSanList = new StringBuilder();
				rawSanList.Append("Raw SAN List:\n");
				foreach (var sanType in san.Keys)
				{
					rawSanList.Append($"SAN Type: {sanType}. Values: ");
					foreach (var indivSan in san[sanType])
					{
						rawSanList.Append($"{indivSan},");
					}
					rawSanList.Append('\n');
				}
				Logger.Trace(rawSanList.ToString());
	
				var sanDict = new Dictionary<string, string[]>(san, StringComparer.OrdinalIgnoreCase);
				Logger.Trace($"DNS SAN Count: {sanDict["dns"].Count()}");
				if (commonName == null)
				{
					foreach (string dnsSan in sanDict["dns"])
					{
						var tempDomain = validDomains.Where(d => dnsSan.EndsWith(d.DomainName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
						if (tempDomain != null)
						{
							Logger.Debug($"SAN Domain match found for SAN: {dnsSan}");
							domain = tempDomain;
							commonName = dnsSan;
							break;
						}
					}
				}
				else
				{
					domain = validDomains.Where(d => commonName.EndsWith(d.DomainName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
				}

				if (domain == null)
				{
					throw new Exception("Unable to determine GlobalSign domain");
				}

				Logger.Debug($"Domain info:\nDomain Name: {domain?.DomainName}\nMsslDomainId: {domain?.DomainID}\nMsslProfileId: {domain?.MSSLProfileID}");
				Logger.Debug($"Using common name: {commonName}");
				var months = productInfo.ProductParameters["Lifetime"];
				Logger.Debug($"Using validity: {months} months.");

				List<string> sanList = new List<string>();
				foreach (string dnsSan in sanDict["dns"])
				{
					sanList.Add(dnsSan);
				}

				var productType = GlobalSignCertType.AllTypes.Where(x => x.ProductCode.Equals(productInfo.ProductID, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

				CAConnectorCertificate priorCert = null;
				switch (enrollmentType)
				{
					case RequestUtilities.EnrollmentType.New:

						Logger.Debug($"Issuing new certificate request for product code {productType.ProductCode}");
						GlobalSignEnrollRequest request = new GlobalSignEnrollRequest(Config)
						{
							MsslDomainId = domain?.DomainID,
							MsslProfileId = domain?.MSSLProfileID,
							CSR = csr,
							Licenses = "1",
							OrderKind = "new",
							Months = months,
							FirstName = requesterName,
							LastName = requesterName,
							Email = domain?.ContactInfo?.Email,
							Phone = domain?.ContactInfo?.Phone,
							CommonName = commonName,
							ProductCode = productType.ProductCode,
							SANs = sanList,
						};

						return apiClient.Enroll(request);

					case RequestUtilities.EnrollmentType.Renew:

						priorCert = certificateDataReader.GetCertificateRecord(CSS.Common.DataConversion.HexToBytes(priorSn));
						Logger.Debug($"Issuing certificate renewal request for cert with request ID {priorCert.CARequestID} and product code {productType.ProductCode}");
						GlobalSignRenewRequest renewRequest = new GlobalSignRenewRequest(Config)
						{
							MsslDomainId = domain?.DomainID,
							MsslProfileId = domain?.MSSLProfileID,
							CSR = csr,
							Licenses = "1",
							OrderKind = "renewal",
							Months = months,
							FirstName = requesterName,
							LastName = requesterName,
							Email = domain?.ContactInfo?.Email,
							Phone = domain?.ContactInfo?.Phone,
							CommonName = commonName,
							ProductCode = productType.ProductCode,
							RenewalTargetOrderId = priorCert.CARequestID,
							SANs = sanList
						};

						return apiClient.Renew(renewRequest);

					case RequestUtilities.EnrollmentType.Reissue:
						priorCert = certificateDataReader.GetCertificateRecord(CSS.Common.DataConversion.HexToBytes(priorSn));
						Logger.Debug($"Issuing certificate reissue request for cert with request ID {priorCert.CARequestID}");
						GlobalSignReissueRequest reissueRequest = new GlobalSignReissueRequest(Config)
						{
							CSR = csr,
							OrderID = priorCert.CARequestID,
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
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
			string syncType = certificateAuthoritySyncInfo.DoFullSync ? "full" : "incremental";
			Logger.Debug($"Performing {syncType} sync");
			try
			{
				GlobalSignApiClient apiClient = new GlobalSignApiClient(Config);

				DateTime fullSyncFrom = new DateTime(2000, 01, 01);
				if (!string.IsNullOrEmpty(Config.SyncStartDate))
				{
					fullSyncFrom = DateTime.Parse(Config.SyncStartDate);
				}

				DateTime? syncFrom = certificateAuthoritySyncInfo.DoFullSync ? fullSyncFrom : certificateAuthoritySyncInfo.OverallLastSync;
				var certs = apiClient.GetCertificatesForSync(certificateAuthoritySyncInfo.DoFullSync, syncFrom, fullSyncFrom, Config.SyncIntervalDays);

				foreach (var c in certs)
				{
					GlobalSignOrderStatus orderStatus = (GlobalSignOrderStatus)Enum.Parse(typeof(GlobalSignOrderStatus), c.CertificateInfo.CertificateStatus);
					DateTime? subDate = DateTime.TryParse(c.OrderInfo?.OrderDate, out DateTime orderDate) ? orderDate : (DateTime?)null;
					DateTime? resDate = DateTime.TryParse(c.OrderInfo?.OrderCompleteDate, out DateTime completeDate) ? completeDate : (DateTime?)null;
					DateTime? revDate = DateTime.TryParse(c.OrderInfo?.OrderDeactivatedDate, out DateTime deactivateDate) ? deactivateDate : (DateTime?)null;
					var certToAdd = new CAConnectorCertificate()
					{
						CARequestID = c.OrderInfo?.OrderId,
						ProductID = c.OrderInfo?.ProductCode,
						SubmissionDate = subDate,
						ResolutionDate = resDate,
						Status = OrderStatus.ConvertToKeyfactorStatus(orderStatus),
						CSR = c.Fulfillment?.OriginalCSR,
						Certificate = c.Fulfillment?.ServerCertificate?.X509Cert,
						RevocationReason = 0,
						RevocationDate = orderStatus == GlobalSignOrderStatus.Revoked ? revDate : new DateTime?()
					};
					Logger.Trace($"Syncronization: Adding certificate with request ID {c.OrderInfo?.OrderId} to the results");
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
			Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
		}

		public override int Revoke(string caRequestID, string hexSerialNumber, uint revocationReason)
		{
			Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
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

			if (!string.IsNullOrEmpty(validateConfig.SyncStartDate))
			{
				_ = DateTime.Parse(validateConfig.SyncStartDate);
				if (validateConfig.SyncIntervalDays <= 0)
				{
					throw new Exception("SyncIntervalDays must be a value greater than 0 when using SyncStartDate");
				}
			}
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

		#endregion Obsolete Methods

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

		#endregion Private Methods
	}
}