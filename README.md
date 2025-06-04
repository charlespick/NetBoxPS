# NetBoxPS

A PowerShell module to interact with the NetBox API. Useful for pulling data out of Microsoft infrastructure systems (AD, MECM, Azure, SCVMM, SCOM, Windows Server DNS, DHCP, IPAM servers, Network ATC, Microsoft SDN, Hyper-V, Failover Clustering, and more) and pushing that data to NetBox

This module is under development, I am adding new API endpoints as I need them. Feel free to open a PR for new functions if you want to build them. Adding support for new functions is [very easy](#️-new-functions) with the built-in helper functions. 

## 🔧 Setup

```powershell
Import-Module NetBoxPS
Set-NBConfig -BaseUrl "https://netbox.example.com/" -ApiToken "your-token"
```

## ✏️ New Functions
The module contains 4 core wrappers that make it minimal work to add support for a new type of object.

The `Invoke-NBGet` and `Invoke-NBPost` wrappers take query or payload data in a hashable Powershell object and the domain of the Netbox object and construct the HTTP requests including authentication, JSON encoding, pagination, and case normalization. Most functions in this module should use these functions to send their final payload or query to the API.

The `ConvertTo-NBQuery` and `ConvertTo-NBPayload` functions are more optional but still very powerful. They take `$PSBoundParameters` provided to the function and construct the filters or payload object using only the parameters used. The resulting object can be passed directly to the `Invoke-` functions.

Create a new file in /Public for every object type. All files in /Public are automatically sourced into the module. 

```powershell
# Example of adding support for querying objects of a new domain

# Powershell standard verb usage
function Get-NBDevice {
    [CmdletBinding()]
    param (
        # This is not an exaustive list of available device filters for demonstration purposes
        [string[]]$AssetTag,
        [string[]]$Description,
        [int[]]$Id,
        [string[]]$Manufacturer,
        [string[]]$Model,
        [string[]]$Name,
        [string[]]$Region,
        [string[]]$Role,
        [string[]]$Serial,
        [string[]]$Site,
        [string[]]$Status,
        [string[]]$Tag,
    )

    # Build filters and make API Call
    $filters = ConvertTo-NBQuery $PSBoundParameters
    Invoke-NBGet -Domain "dcim/devices" -Filters $filters
}

# New- endpoints are similar
function New-NBDevice {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$Device_Type,
        [Parameter(Mandatory)][int]$Role,
        [ValidateSet("offline", "active", "planned", "staged", "failed", "inventory")][string]$Status,
        [array]$Tags,
        [hashtable]$Custom_Fields
    )

    $payload = ConvertTo-NBPayload $PSBoundParameters
    Invoke-NBPost -Domain "dcim/devices" -Payload $payload
}

# Export functions at the end of the file
Export-ModuleMember -Function Get-NBDevice, New-NBDevice

```