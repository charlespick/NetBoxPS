function Get-NBSite {
    param (
        [Parameter(Mandatory)][string]$Name
    )
    Invoke-NBGet -Domain "dcim/sites" -Filters @{ name = $Name }
}

function New-NBSite {
    param (
        [Parameter(Mandatory)][string]$Name,
        [string]$Slug,
        [string]$Description
    )

    $Payload = @{
        name        = $Name
        slug        = $Slug
        description = $Description
    }

    Invoke-NBPost -Domain "dcim/sites" -Payload $Payload
}

Export-ModuleMember -Function Get-NBSite, New-NBSite
