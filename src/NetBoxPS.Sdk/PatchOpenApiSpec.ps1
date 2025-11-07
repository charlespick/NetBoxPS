param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    # Optional knobs (defaults match your schema)
    [string]$SchemaName = 'DataSource',
    [string]$TypePropertyName = 'type',
    [string]$ValuePropertyName = 'value',
    [string]$LabelPropertyName = 'label',
    [string[]]$LabelEnumVarNames = @('None','Local','Git','AmazonS3')
)

$ErrorActionPreference = 'Stop'

function Write-Utf8NoBom {
    param([string]$FilePath, [string]$Content)
    $enc = New-Object System.Text.UTF8Encoding($false)   # no BOM
    [System.IO.File]::WriteAllText($FilePath, $Content, $enc)
}

try {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Spec not found: $Path"
    }

    # NOTE: Windows PowerShell 5.1 has no -Depth on ConvertFrom-Json
    $json = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json

    # Walk the tree defensively
    $components = $json.components
    if ($null -eq $components) { Write-Utf8NoBom -FilePath $Path -Content ($json | ConvertTo-Json -Depth 100); exit 0 }

    $schemas = $components.schemas
    if ($null -eq $schemas) { Write-Utf8NoBom -FilePath $Path -Content ($json | ConvertTo-Json -Depth 100); exit 0 }

    if (-not ($schemas.PSObject.Properties.Name -contains $SchemaName)) { Write-Utf8NoBom -FilePath $Path -Content ($json | ConvertTo-Json -Depth 100); exit 0 }
    $schema = $schemas.$SchemaName

    $props = $schema.properties
    if ($null -eq $props) { Write-Utf8NoBom -FilePath $Path -Content ($json | ConvertTo-Json -Depth 100); exit 0 }

    $typeObj = $props.$TypePropertyName
    if ($null -eq $typeObj) { Write-Utf8NoBom -FilePath $Path -Content ($json | ConvertTo-Json -Depth 100); exit 0 }

    $typeProps = $typeObj.properties
    if ($null -eq $typeProps) { Write-Utf8NoBom -FilePath $Path -Content ($json | ConvertTo-Json -Depth 100); exit 0 }

    # 1) Give C# a valid identifier for the label enum (wire values unchanged)
    if ($typeProps.PSObject.Properties.Name -contains $LabelPropertyName) {
        $labelNode = $typeProps.$LabelPropertyName
        if ($null -ne $labelNode) {
            # Add or overwrite the vendor extension (hyphenated name)
            $labelNode | Add-Member -MemberType NoteProperty -Name 'x-enum-varnames' -Value $LabelEnumVarNames -Force
        }
    }

    # 2) Remove literal null from the 'value' enum at this node only
    if ($typeProps.PSObject.Properties.Name -contains $ValuePropertyName) {
        $valueNode = $typeProps.$ValuePropertyName
        if ($null -ne $valueNode -and $null -ne $valueNode.enum) {
            $valueNode.enum = @($valueNode.enum | Where-Object { $_ -ne $null })
        }
    }

    # Write back as UTF-8 *without BOM*
    $out = $json | ConvertTo-Json -Depth 100
    Write-Utf8NoBom -FilePath $Path -Content $out
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
