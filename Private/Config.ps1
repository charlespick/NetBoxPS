$Script:NetBoxBaseUrl = ""
$Script:NetBoxApiToken = ""

function Set-NBConfig {
    param (
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$ApiToken
    )
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
