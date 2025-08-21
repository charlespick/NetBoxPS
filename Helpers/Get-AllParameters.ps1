# Requires: PowerShell 7+ and the powershell-yaml module for YAML parsing
# Install-Module powershell-yaml -Scope CurrentUser

Import-Module powershell-yaml -ErrorAction SilentlyContinue | Out-Null

. "$PSScriptRoot\..\Build\BuildHelpers.ps1"

# --- Fetch the OpenAPI schema ---
$schemaUri = "https://demo.netbox.dev/api/schema/"
$response = Invoke-WebRequest -Uri $schemaUri -UseBasicParsing
$raw = [System.Text.Encoding]::UTF8.GetString($response.Content)

# --- Parse YAML first; if that fails, try JSON ---
try {
    $doc = ConvertFrom-Yaml $raw
} catch {
    try { $doc = $raw | ConvertFrom-Json } catch { throw "Failed to parse schema as YAML or JSON." }
}


# Collect parameter/property info
$results = @{}

# 1) Object schema properties
if ($doc.components.schemas) {
    foreach ($schemaName in $doc.components.schemas.Keys) {
        $schema = $doc.components.schemas[$schemaName]
        if ($schema.type -eq 'object' -and $schema.properties) {
            foreach ($prop in $schema.properties.Keys) {
                if (-not $results.ContainsKey($prop)) {
                    $results[$prop] = @{ Name = $prop; Type = 'property'; Count = 0 }
                }
                $results[$prop].Count++
            }
        }
    }
}

# 2) Endpoint parameters + request-body fields
if ($doc.paths) {
    foreach ($endpoint in $doc.paths.Keys) {
        $pathItem = $doc.paths[$endpoint]
        foreach ($method in $pathItem.Keys) {
            $methodData = $pathItem[$method]

            # Path/query parameters
            if ($methodData.parameters) {
                foreach ($param in $methodData.parameters) {
                    $n = $param.name
                    if (-not $results.ContainsKey($n)) {
                        $results[$n] = @{ Name = $n; Type = 'param'; Count = 0 }
                    }
                    $results[$n].Count++
                }
            }

            # Request body (JSON) — list top-level field names
            $bodySchema = $methodData.requestBody?.content?.'application/json'?.schema
            if ($null -ne $bodySchema) {
                $fields = Get-BodyPropertyNames -root $doc -schema $bodySchema
                foreach ($f in $fields) {
                    if (-not $results.ContainsKey($f)) {
                        $results[$f] = @{ Name = $f; Type = 'param'; Count = 0 }
                    }
                    $results[$f].Count++
                }
            }
        }
    }
}

# Output to PowerShell table viewer
$results.Values | Sort-Object Name | Select-Object Name, Type, Count | Out-GridView -Title 'NetBox Parameters and Properties'
