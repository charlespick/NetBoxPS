$Script:NetBoxBaseUrl = ""
$Script:NetBoxApiToken = ""

function Set-NBConfig {
    param (
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$ApiToken
    )
    if ($BaseUrl -notmatch "^https://") {
        Write-Error "You must use an HTTPS URL for NetBox. Authentication will fail with non-HTTPS URLs."
        return
    }
    if (-not $ApiToken) {
        Write-Error "API token must be specified and cannot be empty."
    }
    if ($BaseUrl -notmatch "/$") { $BaseUrl += "/" }
    $Script:NetBoxBaseUrl = $BaseUrl
    $Script:NetBoxApiToken = $ApiToken
}

function Get-NBBaseUrl {
    return $Script:NetBoxBaseUrl
}

function Get-NBHeaders {
    return @{
        "Authorization" = "Token $Script:NetBoxApiToken"
        "Accept"        = "application/json"
    }
}

Export-ModuleMember -Function Set-NBConfig
