Import-Module powershell-yaml
$response = Invoke-WebRequest -Uri "https://demo.netbox.dev/api/schema/" -UseBasicParsing
$scriptPath = $MyInvocation.MyCommand.Path
$scriptDirectory = Split-Path -Parent $scriptPath
$outputDirectory = Join-Path -Path $scriptDirectory -ChildPath "..\out"
if (Test-Path $outputDirectory) {
    Remove-Item -Path $outputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDirectory | Out-Null
New-Item -ItemType Directory -Path (Join-Path $outputDirectory 'api') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $outputDirectory 'core') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $outputDirectory 'customobjects') | Out-Null

function Get-FunctionParameters {
    param (
        [Parameter(Mandatory)]
        $methodData
    )

    $params = @()
    $customObjectsList = @()

    # Helper to resolve OpenAPI types to PowerShell types
    function Resolve-PSParameterType {
        param ($schema)

        if ($schema.'$ref') {
            $refName = $schema.'$ref' -replace '^#/components/schemas/', ''
            return (ConvertTo-PascalCase $refName)
        }
        elseif ($schema.type -eq 'array') {
            $itemSchema = $schema.items
            $itemType = Resolve-PSParameterType -schema $itemSchema
            return "$itemType[]"
        }
        elseif ($schema.type) {
            switch ($schema.type) {
                'string' { return 'string' }
                'integer' { return 'int' }
                'boolean' { return 'bool' }
                'number' { return 'double' }
                default { return 'object' }
            }
        }

        return 'object'
    }

    # 1. Path and query parameters
    if ($methodData.parameters) {
        foreach ($param in $methodData.parameters) {
            $paramName = ConvertTo-PascalCase $param.name
            $schema = $param.schema
            $type = Resolve-PSParameterType -schema $schema
            $params += "        [$type]`$$paramName"
        }
    }

    # 2. Body parameters (for POST/PUT/PATCH)
    if ($methodData.requestBody?.content?.'application/json'?.schema) {
        $schema = $methodData.requestBody.content.'application/json'.schema
        $type = Resolve-PSParameterType -schema $schema
        $paramName = if ($type -match '\[\]$') { 'InputObjects' } else { 'InputObject' }

        if ($type -notin @('string', 'int', 'bool', 'double', 'object')) {
            $customObjectsList += $type.TrimEnd('[]')
        }

        $params += "        [$type]`$$paramName"
    }

    return [PSCustomObject]@{
        Parameters    = $params
        CustomObjects = $customObjectsList
    }
}

function ConvertTo-PascalCase {
    param([string]$inputString)
    $parts = $inputString -split '[-_]'
    return ($parts | Where-Object { $_ } | ForEach-Object {
            $_.Substring(0, 1).ToUpper() + $_.Substring(1)
        }) -join ''
}

function Get-PluralizedNoun {
    param([string]$joinedParts)
    if ($joinedParts -match 'ies$') {
        return $joinedParts -replace 'ies$', 'y'
    }
    elseif ($joinedParts -match 'sses$') {
        return $joinedParts -replace 'es$', ''
    }
    elseif ($joinedParts -match 's$') {
        return $joinedParts.TrimEnd('s')
    }
    return $joinedParts
}

function Write-ClassAndConstructor {
    param (
        [string]$className,
        [hashtable]$schema
    )
    $classProps = @()
    $paramEntries = @()
    $assignmentBlock = @()
    $requiredList = if ($schema.ContainsKey("required")) { $schema.required } else { @() }
    foreach ($propName in $schema.properties.Keys) {
        $property = $schema.properties[$propName]
        $psPropName = ConvertTo-PascalCase $propName
        $type = 'string'
        if ($property.'$ref') {
            $type = ConvertTo-PascalCase ($property.'$ref' -replace '^#/components/schemas/', '')
        }
        elseif ($property.type -eq 'array') {
            if ($property.items.'$ref') {
                $itemType = ConvertTo-PascalCase ($property.items.'$ref' -replace '^#/components/schemas/', '')
                $type = "$itemType[]"
            }
            elseif ($property.items.type) {
                $baseType = switch ($property.items.type) {
                    'integer' { 'int' }
                    'string' { 'string' }
                    'boolean' { 'bool' }
                    'number' { 'double' }
                    default { 'object' }
                }
                $type = "$baseType[]"
            }
        }
        elseif ($property.type) {
            $type = switch ($property.type) {
                'integer' { 'int' }
                'string' { 'string' }
                'boolean' { 'bool' }
                'number' { 'double' }
                default { 'object' }
            }
        }
        $classProps += "    [$type]`$$psPropName"
        $paramAttributes = ""
        if ($requiredList -contains $propName) {
            $paramAttributes = "        [Parameter(Mandatory = $([char]0x24 + 'true'))]"
        }
        $paramEntry = @()
        if ($paramAttributes) { $paramEntry += $paramAttributes }
        $paramEntry += "        [$type]`$$psPropName"
        $paramEntries += ($paramEntry -join "`n")
        $assignmentBlock += "    `$obj.$psPropName = `$${psPropName}"
    }
    $paramBlockJoined = ($paramEntries -join ",`n")
    $classDef = @(
        "class $className {",
        ($classProps -join "`n"),
        "}"
    ) -join "`n"
    $constructor = @(
        "function New-$className {",
        "    [CmdletBinding()]",
        "    param(",
        $paramBlockJoined,
        "    )",
        "    `$obj = [$className]::new()",
        ($assignmentBlock -join "`n"),
        "    return `$obj",
        "}"
    ) -join "`n"
    $fileContent = @"
$classDef

$constructor
"@
    $filePath = Join-Path $outputDirectory "customobjects\$className.ps1"
    Set-Content -Path $filePath -Value $fileContent
}

$yamlString = [System.Text.Encoding]::UTF8.GetString($response.Content)
$yaml = ConvertFrom-Yaml $yamlString
foreach ($schemaName in $yaml.components.schemas.Keys) {
    $schema = $yaml.components.schemas[$schemaName]
    if ($schema.type -ne 'object') { continue }
    $className = ConvertTo-PascalCase $schemaName
    Write-ClassAndConstructor -className $className -schema $schema
}
$httpToPsVerbs = @{
    get    = 'Get'
    post   = 'New'
    put    = 'Set'
    patch  = 'Update'
    delete = 'Remove'
}
foreach ($endpoint in $yaml.paths.Keys) {
    if ($endpoint -match '\{.*?\}') {
        continue
    }
    $methods = $yaml.paths[$endpoint].Keys
    $cleanEndpoint = $endpoint.Trim("/")
    if ($cleanEndpoint -like "api/*") {
        $pathWithoutApi = $cleanEndpoint.Substring(4)
        $parts = $pathWithoutApi -split '/'
        $resourceParts = @()
        foreach ($part in $parts) {
            if ($part -match '^\{.*\}$') { break }
            $resourceParts += $part
        }
        if ($resourceParts.Count -eq 0) { continue }
        $mainNoun = $resourceParts[-1]
        $singular = Get-PluralizedNoun $mainNoun
        $pascalNoun = "NB$(ConvertTo-PascalCase $singular)"
        $categoryFile = "$pascalNoun.ps1"
        $functionBlocks = @()
        $functionExports = @()
        foreach ($method in $methods) {
            if ($httpToPsVerbs.ContainsKey($method)) {
                $verb = $httpToPsVerbs[$method]
                $functionName = "$verb-$pascalNoun"
                $functionExports += $functionName
                $methodData = $yaml.paths[$endpoint][$method]
                $result = Get-FunctionParameters -methodData $methodData
                $paramList = $result.Parameters
                $customList = $result.CustomObjects
                $formattedParams = if ($paramList.Count -gt 0) {
                    "    param(`n" + ($paramList -join ",`n") + "`n    )"
                }
                else {
                    "    param()"
                }
                $coreFunction = switch ($method) {
                    'get' { 'Invoke-NBGet' }
                    'post' { 'Invoke-NBPost' }
                    'put' { 'Invoke-NBPut' }
                    'patch' { 'Invoke-NBPatch' }
                    'delete' { 'Invoke-NBDelete' }
                }
                $coreArgs = switch ($method) {
                    'get' { '@{ Filters = (ConvertTo-NBQuery $PSBoundParameters) }' }
                    'delete' { '@{ Filters = (ConvertTo-NBQuery $PSBoundParameters) }' }
                    default { '@{ Payload = (ConvertTo-NBPayload $PSBoundParameters) }' }
                }
                $coreCall = if ($method -in @('get', 'delete')) {
                    "    `$filters = ConvertTo-NBQuery `$PSBoundParameters`n    $coreFunction -Domain `"$pathWithoutApi`" -Filters `$filters"
                }
                else {
                    "    `$payload = ConvertTo-NBPayload `$PSBoundParameters`n    $coreFunction -Domain `"$pathWithoutApi`" -Payload `$payload"
                }
                $functionCode = @"
function $functionName {
    [CmdletBinding()]
$formattedParams

$coreCall
}
"@
                $functionBlocks += $functionCode
            }
        }
        if ($functionBlocks.Count -gt 0) {
            $functionBlocks += "`nExport-ModuleMember -Function " + ($functionExports -join ', ')
            $apiFilePath = Join-Path (Join-Path $outputDirectory 'api') $categoryFile
            Set-Content -Path $apiFilePath -Value ($functionBlocks -join "`n`n")
        }
    }
}
