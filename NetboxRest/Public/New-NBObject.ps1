function New-NBObject {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ObjectType,

        [Parameter(Mandatory)]
        [hashtable]$Body
    )

    Invoke-NBRestMethod -ObjectType $ObjectType -Method Post -Body $Body
}
