function Invoke-NBGet {
    param (
        [Parameter(Mandatory)][string]$Domain,
        [hashtable]$Filters
    )

    $BaseUrl = Get-NBBaseUrl
    $Headers = Get-NBHeaders

    if (-not $BaseUrl -or -not $Headers["Authorization"]) {
        Write-Error "You must configure the module with Set-NBConfig before making API calls."
        return
    }

    $Uri = "${BaseUrl}api/$Domain/"
    if ($Filters) {
        $Query = ($Filters.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "&"
        $Uri += "?$Query"
    }

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

    $BaseUrl = Get-NBBaseUrl
    $Headers = Get-NBHeaders

    if (-not $BaseUrl -or -not $Headers["Authorization"]) {
        Write-Error "You must configure the module with Set-NBConfig before making API calls."
        return
    }

    $Headers["Content-Type"] = "application/json"
    $Uri = "${BaseUrl}api/$Domain/"

    try {
        return Invoke-RestMethod -Method Post -Uri $Uri -Headers $Headers -Body ($Payload | ConvertTo-Json -Depth 10)
    } catch {
        Write-Error "POST request failed on '$Domain': $_"
    }
}