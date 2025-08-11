# NetBoxPS

A PowerShell module to interact with the NetBox API. Useful for pulling data out of Microsoft infrastructure systems (AD, MECM, Azure, SCVMM, SCOM, Windows Server DNS, DHCP, IPAM servers, Network ATC, Microsoft SDN, Hyper-V, Failover Clustering, and more) and pushing that data to NetBox

The code in this repo does not function as a powershell module. It is meant to *build* the powershell module. Refer to the Build directory.

## 🔧 Setup

```powershell
Import-Module NetBoxPS
Set-NBConfig -BaseUrl "https://netbox.example.com/" -ApiToken "your-token"
```
