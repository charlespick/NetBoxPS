function Update-NBObject {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        [Alias('NBObjectType')]
        [string]$ObjectType,

        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        [Alias('NBObjectID')]
        [int]$ID,

        [Parameter(Mandatory)]
        [hashtable]$Body
    )

    process {
        Invoke-NBRestMethod -ObjectType $ObjectType -ID $ID -Method Put -Body $Body
    }
}
