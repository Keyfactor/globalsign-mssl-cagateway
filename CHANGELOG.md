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
