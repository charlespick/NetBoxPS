# NetBoxPS

This is a powershell module for interacting with NetBox via the REST API

## Getting started

1. `$PSVersionTable` Ensure you are running Powershell 7+
2. `Install-Module NetboxRest` Install NetboxRest
3. `Initialize-NBServerConfig -Host "netbox.domain.com" -APIToken (Read-Host -AsSecureString "Enter API Token")` Configure 
4. `Get-NBObject -ObjectType "dcim/devices"` Example command to get devices from NetBox

## Concept

NetboxRest is a thin wrapper around the NetBox API that provides just enough structure for standard operations while making the project not a nightmare to maintain. 

### Object Types

`Get-NBObject -ObjectType "dcim/devices"`

Specify the `-ObjectType` parameter with the relative /api path from the NetBox API documentation. You can find the full API schema of your instance at `https://your-instance-hostname/api/schema/swagger-ui/` (replace with your NetBox URL).

### NBObject

`Get-NBObject -ObjectType "dcim/devices"` returns an array of objects

`Get-NBObject -ObjectType "dcim/devices" -ID 13` returns 1 object accessed by ID

`Get-NBObject -ObjectType "dcim/devices" -Filter @{ name = "server01"; status = "active" }` returns an array of objects filtered by the specified parameters

### Operations

- `Get-NBObject` - Retrieve objects from NetBox (All, by ID, or filtered)
- `New-NBObject` - Create new objects in NetBox
- `Set-NBObject` - Update only the specified properties of existing objects in NetBox (by ID)
- `Update-NBObject` - Replace existing objects in NetBox with the exact properties (by ID)
- `Remove-NBObject` - Delete objects from NetBox (by ID)

### Piping

When you pipe an object into `Set-NBObject`, `Update-NBObject`, or `Remove-NBObject`, the cmdlet will automatically pass the ObjectType and ID parameters from `Get-NBObject`.

### Initialize-NBServerConfig

`Initialize-NBServerConfig -Host "netbox.domain.com" -APIToken (Read-Host -AsSecureString "Enter API Token")`

This command initializes the server configuration into memory for the current session. You can also save the configuration to disk for future sessions by adding the `-SaveToDisk` parameter. The configuration (including API key) is saved to a file in the user's home directory named `.netboxrestconfig.json` in PLAIN TEXT (be careful).

If you call a NetboxRest cmdlet and no server configuration is found in memory, it will attempt to load the configuration from the `.netboxrestconfig.json` file in the user's home directory. Otherwise you will get an error indicating no server configuration is found.

## Example Usage

```powershell
# Create a device
New-NBObject -ObjectType "dcim/devices" -Body @{
    name = "server02"
    status = "active"
    site = 1
    device_type = 3
    role = 9
}

# Update via pipeline
Get-NBObject -ObjectType "dcim/devices" -ID 13 |
Set-NBObject -Body @{ status = "offline" }

# Delete via pipeline
Get-NBObject -ObjectType "dcim/devices" -Filter @{ name = "server02" } |
Remove-NBObject

# List devices
Get-NBObject -ObjectType "dcim/devices"

# Filter devices
Get-NBObject -ObjectType "dcim/devices" -Filter @{ status = "active" }

# Filter using Django-style lookups
Get-NBObject -ObjectType "dcim/devices" -Filter @{ name__icontains = "router" }
```