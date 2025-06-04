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
        $queryParts = @()

        foreach ($entry in $Filters.GetEnumerator()) {
            $key = $entry.Key.ToLower()
            $value = $entry.Value

            if ($value -is [System.Collections.IEnumerable] -and -not ($value -is [string])) {
                foreach ($val in $value) {
                    $queryParts += "$key=$val"
                }
            } else {
                $queryParts += "$key=$value"
            }
        }

        if ($queryParts.Count -gt 0) {
            $Uri += "?" + ($queryParts -join "&")
        }
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

    # Normalize payload keys to lowercase
    $NormalizedPayload = @{}

    foreach ($entry in $Payload.GetEnumerator()) {
        $key = $entry.Key.ToLower()
        $value = $entry.Value
        $NormalizedPayload[$key] = $value
    }
    try {
        return Invoke-RestMethod -Method Post -Uri $Uri -Headers $Headers -Body ($NormalizedPayload | ConvertTo-Json -Depth 10)
    } catch {
        Write-Error "POST request failed on '$Domain': $_"
    }
}

function ConvertTo-NBQuery {
    param (
        [hashtable]$Params
    )

    $query = @{}
    $Params.GetEnumerator() | ForEach-Object {
        if ($_.Key -notin @('Verbose', 'Debug', 'ErrorAction', 'WarningAction', 'InformationAction')) {
            $key = $_.Key.ToLower()
            $query[$key] = $_.Value
        }
    }

    return $query
}

function ConvertTo-NBPayload {
    param (
        [hashtable]$Params
    )

    $payload = @{}
    $Params.GetEnumerator() | ForEach-Object {
        if ($_.Key -notin @('Verbose', 'Debug', 'ErrorAction', 'WarningAction', 'InformationAction')) {
            $key = $_.Key.ToLower()
            $payload[$key] = $_.Value
        }
    }

    return $payload
}
