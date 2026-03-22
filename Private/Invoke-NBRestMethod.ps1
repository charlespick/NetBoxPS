function Invoke-NBRestMethod {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ObjectType,

        [Parameter()]
        [int]$ID,

        [Parameter(Mandatory)]
        [Microsoft.PowerShell.Commands.WebRequestMethod]$Method,

        [Parameter()]
        [hashtable]$Body,

        [Parameter()]
        [hashtable]$Filter
    )

    $config = Get-NBServerConfig

    $scheme = if ($config.UseInsecureHttp) { 'http' } else { 'https' }
    $baseUrl = "${scheme}://$($config.Host)/api/$ObjectType/"

    if ($ID) {
        $baseUrl = "${baseUrl}${ID}/"
    }

    if ($Filter -and $Filter.Count -gt 0) {
        $queryParts = foreach ($key in $Filter.Keys) {
            $encodedKey = [System.Uri]::EscapeDataString($key)
            $encodedValue = [System.Uri]::EscapeDataString($Filter[$key])
            "${encodedKey}=${encodedValue}"
        }
        $baseUrl = "${baseUrl}?$($queryParts -join '&')"
    }

    $params = @{
        Uri         = $baseUrl
        Method      = $Method
        Headers     = @{ Authorization = "Token $($config.APIToken)" }
        ContentType = 'application/json'
        ErrorAction = 'Stop'
    }

    if ($config.IgnoreCertErrors) {
        $params['SkipCertificateCheck'] = $true
    }

    if ($Body) {
        $params['Body'] = $Body | ConvertTo-Json -Depth 20
    }

    $response = Invoke-RestMethod @params

    # Handle pagination for GET requests
    if ($Method -eq 'Get' -and $response.PSObject.Properties.Name -contains 'results') {
        $allResults = [System.Collections.Generic.List[object]]::new()
        $allResults.AddRange([object[]]$response.results)

        $nextUrl = $response.next
        while ($nextUrl) {
            $nextParams = @{
                Uri         = $nextUrl
                Method      = 'Get'
                Headers     = @{ Authorization = "Token $($config.APIToken)" }
                ContentType = 'application/json'
                ErrorAction = 'Stop'
            }
            if ($config.IgnoreCertErrors) {
                $nextParams['SkipCertificateCheck'] = $true
            }
            $response = Invoke-RestMethod @nextParams
            $allResults.AddRange([object[]]$response.results)
            $nextUrl = $response.next
        }

        return $allResults
    }

    return $response
}
