
#region Setup & Utilities
Import-Module powershell-yaml
. "$PSScriptRoot\BuildHelpers.ps1"

$response = Invoke-WebRequest -Uri "https://demo.netbox.dev/api/schema/" -UseBasicParsing
$scriptPath = $MyInvocation.MyCommand.Path
$scriptDirectory = Split-Path -Parent $scriptPath
$outputDirectory = Join-Path -Path $scriptDirectory -ChildPath "..\out"

# Reset output directory
if (Test-Path $outputDirectory) {
    Remove-Item -Path $outputDirectory -Recurse -Force
}
foreach ($sub in @('api', 'core', 'customobjects')) {
    New-Item -ItemType Directory -Path (Join-Path $outputDirectory $sub) | Out-Null
}

$yamlString = [System.Text.Encoding]::UTF8.GetString($response.Content)
$yaml = ConvertFrom-Yaml $yamlString
#endregion

#region Build Classes
$definedTypes = @{}
foreach ($schemaName in $yaml.components.schemas.Keys) {
    $schema = $yaml.components.schemas[$schemaName]
    if ($schema.type -ne 'object') { continue }

    $definedTypes[$schemaName] = $schema

    Build-Class -className $schemaName -schema $schema -outputDirectory $outputDirectory
}
#endregion

#region Build Endpoint Functions
foreach ($endpoint in $yaml.paths.Keys) {
    if ($endpoint -match '\{.*?\}') { continue }

    $methods = $yaml.paths[$endpoint].Keys
    $cleanEndpoint = $endpoint.Trim("/")

    if ($cleanEndpoint -like "api/*") {
        $pathWithoutApi = $cleanEndpoint.Substring(4)
        $parts = $pathWithoutApi -split '/'

        # Build noun from path parts (stop at first parameterized segment)
        $resourceParts = @()
        foreach ($part in $parts) {
            if ($part -match '^\{.*\}$') { break }
            $resourceParts += $part
        }
        if ($resourceParts.Count -eq 0) { continue }

        $mainNoun = $resourceParts[-1]

        $singleNoun = Get-SingularNoun $mainNoun
        $pascalNoun = "NB$(ConvertTo-PascalCase $singleNoun)"

        foreach ($method in $methods) {
            if ($httpToPsVerbs.ContainsKey($method)) {
                $methodData = $yaml.paths[$endpoint][$method]

                $result = Get-FunctionParameters -methodData $methodData
                $customTypes = $result.CustomObjects

                # Validate custom types exist
                $undefinedTypes = @()
                foreach ($type in $customTypes) {
                    if (-not $definedTypes.ContainsKey($type)) {
                        $undefinedTypes += $type
                    }
                }
                if ($undefinedTypes.Count -gt 0) {
                    Write-Warning "Endpoint $endpoint ($method) uses undefined types: $($undefinedTypes -join ', ')"
                    continue
                }

                Build-EndpointFunction -MethodData $methodData -FunctionName $pascalNoun -OutputDirectory $outputDirectory
            }
        }
    }
}
#endregion
