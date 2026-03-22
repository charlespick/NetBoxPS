function Remove-NBObject {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        [Alias('NBObjectType')]
        [string]$ObjectType,

        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        [Alias('NBObjectID')]
        [int]$ID
    )

    process {
        if ($PSCmdlet.ShouldProcess("$ObjectType ID $ID", 'Delete')) {
            Invoke-NBRestMethod -ObjectType $ObjectType -ID $ID -Method Delete
        }
    }
}
