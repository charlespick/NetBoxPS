function Get-NBObject {
    [CmdletBinding(DefaultParameterSetName = 'All')]
    param(
        [Parameter(Mandatory)]
        [string]$ObjectType,

        [Parameter(ParameterSetName = 'ByID')]
        [int]$ID,

        [Parameter(ParameterSetName = 'Filter')]
        [hashtable]$Filter
    )

    $params = @{
        ObjectType = $ObjectType
        Method     = 'Get'
    }

    if ($PSBoundParameters.ContainsKey('ID')) {
        $params['ID'] = $ID
    }

    if ($PSBoundParameters.ContainsKey('Filter')) {
        $params['Filter'] = $Filter
    }

    $results = Invoke-NBRestMethod @params

    # Attach ObjectType and ID as NoteProperties for pipeline support
    foreach ($item in $results) {
        $item | Add-Member -NotePropertyName 'NBObjectType' -NotePropertyValue $ObjectType -Force
        if ($item.id) {
            $item | Add-Member -NotePropertyName 'NBObjectID' -NotePropertyValue $item.id -Force
        }
    }

    return $results
}
