## Overview

The GlobalSign AnyGateway plugin extends the capabilities of GlobalSign's Managed SSL/TLS product to Keyfactor Command via the Keyfactor AnyGateway. The plugin represents a fully featured AnyGateway Plugin with the following capabilies:
* SSL Certificate Synchronization
* SSL Certificate Enrollment
* SSL Certificate Revocation


## Requirements

The GlobalSign API can filter requests based on IP address. Ensure that the appropriate IP address has been whitelisted on your GlobalSign account to allow API requests.
This AnyGateway plugin uses the contact information of the GCC Domain point of contact when enrolling for certificates.  These fields are required to submit and enrollment and must be populated on the Domain's point of contact. This can be found in the GlobalSign Portal in the Manage Domains page. 

## Gateway Registration

In order to enroll for certificates the Keyfactor Command server must trust the trust chain. Once you identify your Root and/or Subordinate CA in your GlobalSign account, make sure to download and import the certificate chain into the Command Server certificate store

## Certificate Template Creation Step

Note for SMIME product types (Secure Email types): The template configuration fields provided for those are not required to be filled out in the gateway config. Many of those values would change on a per-enrollment basis. The way to handle that is to create Enrollment fields in Command with the same name (for example: CommonNameIndicator) and then any values populated in those fields will override any static values provided in the configuration.

