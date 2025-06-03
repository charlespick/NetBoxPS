function Get-NBDevice {
    param (
        [Parameter(Mandatory)][string]$Name
    )
    Invoke-NBGet -Domain "dcim/devices" -Filters @{ name = $Name }
}

function New-NBDevice {
    param (
        [Parameter(Mandatory)][int]$DeviceTypeID,
        [Parameter(Mandatory)][int]$DeviceRoleID,
        [Parameter(Mandatory)][int]$SiteID,
        [Parameter(Mandatory)][string]$Name,
        [string]$Serial,
        [string]$Description
    )

    $Payload = @{
        device_type = $DeviceTypeID
        role        = $DeviceRoleID
        site        = $SiteID
        name        = $Name
        serial      = $Serial
        description = $Description
    }

    Invoke-NBPost -Domain "dcim/devices" -Payload $Payload
}

Export-ModuleMember -Function Get-NBDevice, New-NBDevice
