@{
    RootModule        = 'NetboxRest.psm1'
    ModuleVersion     = '0.1.1'
    GUID              = 'a3e6b2d4-8f1c-4e5a-9d7b-2c8f0e3a1b5d'
    Author            = 'Charles Pick'
    Description       = 'A PowerShell module for interacting with NetBox via the REST API'
    PowerShellVersion = '7.0'
    FunctionsToExport = @(
        'Initialize-NBServerConfig'
        'Get-NBObject'
        'New-NBObject'
        'Set-NBObject'
        'Update-NBObject'
        'Remove-NBObject'
    )
    CmdletsToExport   = @()
    VariablesToExport  = @()
    AliasesToExport    = @()
    PrivateData        = @{
        PSData = @{
            Tags       = @('NetBox', 'API', 'REST', 'DCIM', 'IPAM')
            ProjectUri = 'https://github.com/charlespick/NetboxPS'
        }
    }
}
