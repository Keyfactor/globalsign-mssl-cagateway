1.0.0
Inital Release.  Support for Enroll, Sync, and Revocation. 

1.0.5
Fix bug where certain domains would not get parsed correctly.

1.0.9
Use DNS SAN in place of CN if present for domain lookup and enrollment

1.0.10
Add additional logging output

1.0.11
Convert GlobalSign status codes to Keyfactor status codes for syncing

1.0.12
Fix authentication bug when picking up certificates

1.0.15
Better datetime parsing of returned certificates

1.0.16
Fix for adding additional SANs to certificate requests

1.1.0
Add ability to page inventory  
Fix to remove AD-dependence  

1.1.1
Hotfixes for BaseOption flag for Renewal workflow  
Hotfix for domain lookup  

1.1.2
Hotfix for renewal workflow  

1.2.0
Add SyncProducts config to filter certificate sync by product ID  
Add ability to manually specify MSSLProfileID per template to use for domain lookup  
Bugfix: Treat SANs that match the base domain of a wildcard CN as identical for the purpose of removing duplicates  

1.3.0  
Remove retry logic on reissue, return ExternalValidation if cert is not immediately ready
