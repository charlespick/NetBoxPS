# NetBoxPS

A PowerShell module to interact with the NetBox API. Useful for pulling data out of Microsoft infrastructure systems (AD, MECM, Azure, SCVMM, SCOM, Windows Server DNS, DHCP, IPAM servers, Network ATC, Microsoft SDN, Hyper-V, Failover Clustering, and more) and pushing that data to NetBox

This module is under development, I am adding new API endpoints as I need them. Feel free to open a PR for new functions if you want to build them. Adding support for new functions is [very easy](#️-new-functions) with the built-in helper functions. 

## 🔧 Setup

```powershell
Import-Module NetBoxPS
Set-NBConfig -BaseUrl "https://netbox.example.com/" -ApiToken "your-token"
```

## ✏️ New Functions
The `Invoke-NBGet` and `Invoke-NBPost` wrappers are designed to make adding support for new API endpoints easier.
```powershell
# Example of adding support for adding objects of a new domain
# This is currently unfinished and now all paremeters are supported

# Powershell standard verb usage
function New-NBDevice {
    # Specify parameters according to API documentation
    param (
        [Parameter(Mandatory)][int]$DeviceTypeID,
        [Parameter(Mandatory)][int]$DeviceRoleID,
        [Parameter(Mandatory)][int]$SiteID,
        [Parameter(Mandatory)][string]$Name,
        [string]$Serial,
        [string]$Description
    )

    # (Optional) internal logic to prepare payload data, if needed

    # Construct payload according to provided parameters
    $Payload = @{
        device_type = $DeviceTypeID
        role        = $DeviceRoleID
        site        = $SiteID
        name        = $Name
        serial      = $Serial
        description = $Description
    }

    # Make API Call
    Invoke-NBPost -Domain "dcim/devices" -Payload $Payload
}

# Get endpoints are similar
function Get-NBDevice {
    param (
        [Parameter(Mandatory)][string]$Name
    )
    Invoke-NBGet -Domain "dcim/devices" -Filters @{ name = $Name }
}

# Export functions at the end of the file
Export-ModuleMember -Function Get-NBDevice, New-NBDevice

```