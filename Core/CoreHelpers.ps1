function Invoke-NBGet {
    param (
        [Parameter(Mandatory)][string]$Domain,
        [hashtable]$Filters
    )

    $Uri = "$(Get-NBBaseUrl)api/$Domain/"
    if ($Filters) {
        $Query = ($Filters.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "&"
        $Uri += "?$Query"
    }

    $Headers = Get-NBHeaders
    $Results = @()

    try {
        do {
            $Response = Invoke-RestMethod -Method Get -Uri $Uri -Headers $Headers
            $Results += $Response.results
            $Uri = $Response.next
        } while ($Uri)
        return $Results
    } catch {
        Write-Error "GET request failed on '$Domain': $_"
    }
}

function Invoke-NBPost {
    param (
        [Parameter(Mandatory)][string]$Domain,
        [Parameter(Mandatory)][hashtable]$Payload
    )

    $Uri = "$(Get-NBBaseUrl)api/$Domain/"
    $Headers = Get-NBHeaders
    $Headers["Content-Type"] = "application/json"

    try {
        return Invoke-RestMethod -Method Post -Uri $Uri -Headers $Headers -Body ($Payload | ConvertTo-Json -Depth 10)
    } catch {
        Write-Error "POST request failed on '$Domain': $_"
    }
}
