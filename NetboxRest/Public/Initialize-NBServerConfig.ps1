function Initialize-NBServerConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [Alias('Hostname')]
        [string]$Host,

        [Parameter(Mandatory)]
        [securestring]$APIToken,

        [Parameter()]
        [switch]$SaveToDisk,

        [Parameter()]
        [switch]$UseInsecureHttp,

        [Parameter()]
        [switch]$IgnoreCertErrors
    )

    $plainToken = ConvertFrom-SecureString $APIToken -AsPlainText

    $script:NBConfig = @{
        Host             = $Host
        APIToken         = $plainToken
        UseInsecureHttp  = [bool]$UseInsecureHttp
        IgnoreCertErrors = [bool]$IgnoreCertErrors
    }

    if ($SaveToDisk) {
        $configPath = Join-Path $HOME '.netboxrestconfig.json'
        $script:NBConfig | ConvertTo-Json | Set-Content -Path $configPath -Force
        Write-Verbose "Configuration saved to $configPath"
    }

    Write-Verbose "NetBox server configuration initialized for $Host"
}
