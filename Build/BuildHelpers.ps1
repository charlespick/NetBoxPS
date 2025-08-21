
$httpToPsVerbs = @{
    get    = 'Get'
    post   = 'New'
    put    = 'Set'
    patch  = 'Update'
    delete = 'Remove'
}

function ConvertTo-PascalCase {
    param([string]$inputString)
    if ([string]::IsNullOrWhiteSpace($inputString)) { return $inputString }
    ($inputString -split '[-_/]' | Where-Object { $_ } | ForEach-Object {
        $_.Substring(0, 1).ToUpper() + $_.Substring(1)
    }) -join ''
}

function Resolve-Ref {
    param(
        $schema,
        $root
    )
    if ($schema.'$ref') {
        $refName = $schema.'$ref' -replace '^#/components/schemas/', ''
        return @{
            Name   = $refName
            Schema = $root.components.schemas[$refName]
        }
    }
    return @{
        Name   = $null
        Schema = $schema
    }
}

function Get-BodyPropertyNames {
    param(
        $root,
        $schema
    )
    $resolved = Resolve-Ref -schema $schema -root $root
    $s = $resolved.Schema

    # Array of objects
    if ($s.type -eq 'array' -and $s.items) {
        $itemResolved = Resolve-Ref -schema $s.items -root $root
        $item = $itemResolved.Schema
        if ($item.type -eq 'object' -and $item.properties) {
            return @($item.properties.Keys)
        }
        return @()
    }

    # Direct object
    if ($s.type -eq 'object' -and $s.properties) {
        return @($s.properties.Keys)
    }

    # Referenced object without explicit type
    if ($s.'$ref') {
        $ref = Resolve-Ref -schema $s -root $root
        if ($ref.Schema.properties) { return @($ref.Schema.properties.Keys) }
    }

    return @()
}

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

function Get-SingularNoun {
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

#region Placeholders for generation functions
function Build-Class {
    param (
        [Parameter(Mandatory)][string] $className,
        [Parameter(Mandatory)][hashtable] $schema,
        [Parameter(Mandatory)][string] $outputDirectory
    )

    if (-not $schema.ContainsKey('properties')) {
        Write-Warning "Schema for $className has no properties"
        return
    }

    $lines = @()
    $lines += "class $className {"

    # Generate properties
    $propertyMap = @{}
    foreach ($propName in $schema.properties.Keys) {
        $propDef = $schema.properties[$propName]

        # Map OpenAPI type -> PowerShell type
        $psType = 'string'
        if ($propDef.type) {
            switch ($propDef.type) {
                'integer' { $psType = 'int' }
                'number'  { $psType = 'double' }
                'boolean' { $psType = 'bool' }
                'array'   { $psType = 'object[]' }
                'object'  { $psType = 'hashtable' }
                default   { $psType = 'string' }
            }
        } elseif ($propDef.'$ref') {
            $refName = Split-Path $propDef.'$ref' -Leaf
            $psType = $refName
        }

        $pascalPropName = ConvertTo-PascalCase $propName
        $propertyMap[$pascalPropName] = $propName

        $lines += "    [$psType]`$$pascalPropName"
    }

    # Static property map
    $lines += ""
    $lines += "    hidden static [hashtable] \$PropertyMap = @{"
    foreach ($k in $propertyMap.Keys) {
        $lines += "        $k = '$($propertyMap[$k])'"
    }
    $lines += "    }"
    $lines += ""

    # Constructor
    $lines += "    $className([hashtable] \$init) {"
    $lines += "        foreach (\$prop in [$className]::PropertyMap.Keys) {"
    $lines += "            if (\$init.ContainsKey(\$prop)) {"
    $lines += "                \$this.\$prop = \$init[\$prop]"
    $lines += "            }"
    $lines += "        }"
    $lines += "    }"
    $lines += ""

    # ToHashtable
    $lines += "    [hashtable] ToHashtable() {"
    $lines += "        \$ht = @{}"
    $lines += "        foreach (\$prop in [$className]::PropertyMap.Keys) {"
    $lines += "            \$apiKey = [$className]::PropertyMap[\$prop]"
    $lines += "            \$ht[\$apiKey] = \$this.\$prop"
    $lines += "        }"
    $lines += "        return \$ht"
    $lines += "    }"

    $lines += "}"  # end class
    $lines += ""

    # Factory function
    $lines += "function New-$className {"
    $lines += "    [CmdletBinding()]"
    $lines += "    param ("
    $lines += "        [Parameter(ValueFromPipeline)] [hashtable] \$PropertyBag"
    $lines += "    )"
    $lines += "    process {"
    $lines += "        return [$className]::new(\$PropertyBag)"
    $lines += "    }"
    $lines += "}"
    $lines += ""
    $lines += "Export-ModuleMember -Class $className -Function New-$className"

    # Write file
    $path = Join-Path $outputDirectory "$className.ps1"
    Set-Content -Path $path -Value ($lines -join [Environment]::NewLine)
}

function Build-EndpointFunction {
    param (
        [Parameter(Mandatory)] [string] $endpoint,
        [Parameter(Mandatory)] [string] $method,
        [Parameter(Mandatory)] [hashtable] $methodData,
        [Parameter(Mandatory)] [string] $pascalNoun,
        [Parameter(Mandatory)] [string] $outputDirectory
    )

    # Map HTTP method to PowerShell verb
    $verb = $httpToPsVerbs[$method.ToLower()]
    if (-not $verb) {
        Write-Warning "Unsupported HTTP method '$method' for endpoint '$endpoint'"
        return
    }

    # Build function name
    $functionName = "$verb-$pascalNoun"

    # Gather parameters
    $paramResult   = Get-FunctionParameters -methodData $methodData
    $parameters    = $paramResult.Parameters
    $bodyProps     = Get-BodyPropertyNames -methodData $methodData

    # Figure out which Invoke-NB* function to call
    $invokeTarget = "Invoke-NB$($method.Substring(0,1).ToUpper() + $method.Substring(1).ToLower())"

    # Construct function body
    $lines = @()
    $lines += "function $functionName {"
    $lines += "    [CmdletBinding()]"
    $lines += "    param ("
    $lines += "        [Parameter(Mandatory)][string]\$Domain"

    foreach ($p in $parameters) {
        $lines += "        , [Parameter()][string]\$$p"
    }

    if ($bodyProps.Count -gt 0 -or $method -in @('post','put','patch')) {
        $lines += "        , [Parameter()][hashtable]\$Payload"
    }

    $lines += "    )"
    $lines += ""
    $lines += "    # Build endpoint URI"
    $endpointLiteral = $endpoint
    foreach ($p in $parameters) {
        # Replace path {param} with actual value
        $endpointLiteral = $endpointLiteral -replace "\{$p\}", "`$${p}"
    }
    $lines += "    \$uri = \"\$Domain$endpointLiteral\""

    # Handle payload
    if ($bodyProps.Count -gt 0 -or $method -in @('post','put','patch')) {
        $lines += "    if (-not \$Payload) { \$Payload = @{} }"
    }

    # Call Invoke-NB*
    if ($method -eq 'get' -or $method -eq 'delete') {
        $lines += "    return $invokeTarget -Domain \$Domain -Uri \$uri"
    } else {
        $lines += "    return $invokeTarget -Domain \$Domain -Uri \$uri -Payload \$Payload"
    }

    $lines += "}"
    $lines += "Export-ModuleMember -Function $functionName"

    # Write out to file
    $path = Join-Path $outputDirectory "$functionName.ps1"
    Set-Content -Path $path -Value ($lines -join [Environment]::NewLine)
}
#endregion
