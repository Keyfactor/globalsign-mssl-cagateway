# {{ name }}
## {{ integration_type | capitalize }}

{{ description }}

*** 
## Introduction
This AnyGateway plug enables issuance, revocation, and synchronization of certificates from GlobalSign's Managed SSL/TLS offering.  
## Prerequisites

### Certificate Chain

In order to enroll for certificates the Keyfactor Command server must trust the trust chain. Once you create your Root and/or Subordinate CA, make sure to import the certificate chain into the AnyGateway and Command Server certificate store

### API Allow List
The GlobalSign API can filter requested based on IP address.  Ensure that appropiate IP address is allowed to make requests to the GlobalSign API.

### Domain Point of Contact
This AnyGateway plugin uses the contact information of the GCC Domain point of contact when enrolling for certificates.  These fields are required to submit and enrollment and must be populated on the Domain's point of contact. This can be found in the GlobalSign Portal in the Manage Domains page. 

### Migration
In the event that a system is being upgraded from the Legacy GlobalSign CA Gateway (19.4 or older), a migration from the legacy database format to the AnyGateway format will be required. 

To begin the migration process, copy the GlobalSignEsentMigrator.dll to the Program Files\Keyfactor\Keyfactor AnyGateway directory. Afterwardsm, the DatabaseManagementConsole.exe.config will need to be updated to reference the GlobalSignEsentMigrator.  This is one by modifying the mapping for the IDatabaseMigrator inteface in the config file. 
```xml
<register type="IDatabaseMigrator" mapTo="Keyfactor.AnyGateway.GlobalSign.Database.GlobalSignEsentMigrator, GlobalSignEsentMigrator" />
```


## Install
* Download latest successful build from [GitHub Releases](/releases/latest)

* Copy GloabalSignCAProxy.dll to the Program Files\Keyfactor\Keyfactor AnyGateway directory

* Update the CAProxyServer.config file
  * Update the CAConnection section to point at the GloabalSignCAProxy class
  ```xml
  <alias alias="CAConnector" type="Keyfactor.Extensions.AnyGateway.GlobalSign.GloabalSignCAProxy, GloabalSignCAProxy"/>
  ```

## Configuration
The following sections will breakdown the required configurations for the AnyGatewayConfig.json file that will be imported to configure the AnyGateway.

### Templates
The Template section will map the CA's SSL profile to an AD template. The Lifetime parameter is required and represents the certificate duration in months. 
 ```json
  "Templates": {
	"WebServer": {
      "ProductID": "PEV",
      "Parameters": {
		"Lifetime":"12"
      }
   }
}
 ```
 The following product codes are supported:
 * Extended SSL SHA 256 (PEV_SHA2)
 * Organizational SSL SHA 256 (PV_SHA2)
 * Intranet SSL SHA 1 (PV_INTRA)
 * Intranet SSL SHA 2 (PV_INTRA_SHA2)
 * Intranet SSL SHA 256 ECDSA (PV_INTRA_ECCP256)
 * Cloud SSL SHA 256 (PV_CLOUD)
 * Cloud SSL SHA 256 ECDSA (PV_CLOUD_ECC2)
 
 
### Security
The security section does not change specifically for the Entrust CA Gateway.  Refer to the AnyGateway Documentation for more detail.
```json
  /*Grant permissions on the CA to users or groups in the local domain.
	READ: Enumerate and read contents of certificates.
	ENROLL: Request certificates from the CA.
	OFFICER: Perform certificate functions such as issuance and revocation. This is equivalent to "Issue and Manage" permission on the Microsoft CA.
	ADMINISTRATOR: Configure/reconfigure the gateway.
	Valid permission settings are "Allow", "None", and "Deny".*/
    "Security": {
        "Keyfactor\\Administrator": {
            "READ": "Allow",
            "ENROLL": "Allow",
            "OFFICER": "Allow",
            "ADMINISTRATOR": "Allow"
        },
        "Keyfactor\\gateway_test": {
            "READ": "Allow",
            "ENROLL": "Allow",
            "OFFICER": "Allow",
            "ADMINISTRATOR": "Allow"
        },		
        "Keyfactor\\SVC_TimerService": {
            "READ": "Allow",
            "ENROLL": "Allow",
            "OFFICER": "Allow",
            "ADMINISTRATOR": "None"
        },
        "Keyfactor\\SVC_AppPool": {
            "READ": "Allow",
            "ENROLL": "Allow",
            "OFFICER": "Allow",
            "ADMINISTRATOR": "Allow"
        }
    }
```
### CerificateManagers
The Certificate Managers section is optional.
	If configured, all users or groups granted OFFICER permissions under the Security section
	must be configured for at least one Template and one Requester. 
	Uses "<All>" to specify all templates. Uses "Everyone" to specify all requesters.
	Valid permission values are "Allow" and "Deny".
```json
  "CertificateManagers":{
		"DOMAIN\\Username":{
			"Templates":{
				"MyTemplateShortName":{
					"Requesters":{
						"Everyone":"Allow",
						"DOMAIN\\Groupname":"Deny"
					}
				},
				"<All>":{
					"Requesters":{
						"Everyone":"Allow"
					}
				}
			}
		}
	}
```
### CAConnection
The CA Connection section will determine the API endpoint and configuration data used to connect to GlobalSign MSSL API. 
* ```IsTest```
This determines if the test API endpoints are used with the Gateway.  
* ```PickupRetries```
This is the number of times the AnyGateway will attempt to pickup an new certificate before reporting an error. This setting applies to new, renewed, or reissued certificates. 
* ```PickupDelay```
This is the number of seconds between retries when attempting to download a certificate. 
* ```Username```
This is the username that will be used to connect to the GloabalSign API
* ```Password```
This is the password that will be used to connect to the GloabalSign API

```json
  "CAConnection": {
	"IsTest":"false",
	"PickupRetries":5,
	"PickupDelay":150,
	"Username":"PAR12344_apiuser",
	"Password":"password"
  },
```
### GatewayRegistration
There are no specific Changes for the GatewayRegistration section. Refer to the Refer to the AnyGateway Documentation for more detail.
```json
  "GatewayRegistration": {
    "LogicalName": "GlobalsSignCASandbox",
    "GatewayCertificate": {
      "StoreName": "CA",
      "StoreLocation": "LocalMachine",
      "Thumbprint": "bc6d6b168ce5c08a690c15e03be596bbaa095ebf"
    }
  }
```

### ServiceSettings
There are no specific Changes for the GatewayRegistration section. Refer to the Refer to the AnyGateway Documentation for more detail.
```json
  "ServiceSettings": {
    "ViewIdleMinutes": 8,
    "FullScanPeriodHours": 24,
	"PartialScanPeriodMinutes": 240 
  }
```