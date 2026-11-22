function Initialize-NBServerConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [Alias('Host')]
        [string]$Hostname,

        [Parameter(Mandatory)]
        [securestring]$APIToken,

        [Parameter()]
        [switch]$SaveToDisk,

        [Parameter()]
        [switch]$UseInsecureHttp,

        [Parameter()]
        [switch]$IgnoreCertErrors
    )

    # A missing space (-Host"netbox.example.com") makes PowerShell pass the whole
    # token positionally as "-Hostnetbox.example.com". Catch that instead of
    # saving a hostname that can never resolve.
    if ($Hostname.StartsWith('-')) {
        throw "Hostname '$Hostname' starts with '-'. Did you forget a space after the parameter name? Example: -Hostname `"netbox.example.com`""
    }

    # Accept a pasted URL: strip the scheme and any trailing slash.
    if ($Hostname -match '^(?<scheme>https?)://') {
        if ($Matches.scheme -eq 'http') { $UseInsecureHttp = $true }
        $Hostname = $Hostname -replace '^https?://', ''
    }
    $Hostname = $Hostname.TrimEnd('/')

    $plainToken = ConvertFrom-SecureString $APIToken -AsPlainText

    $script:NBConfig = @{
        Host             = $Hostname
        APIToken         = $plainToken
        UseInsecureHttp  = [bool]$UseInsecureHttp
        IgnoreCertErrors = [bool]$IgnoreCertErrors
    }

    if ($SaveToDisk) {
        $configPath = Join-Path $HOME '.netboxrestconfig.json'
        $script:NBConfig | ConvertTo-Json | Set-Content -Path $configPath -Force
        Write-Verbose "Configuration saved to $configPath"
    }

    Write-Verbose "NetBox server configuration initialized for $Hostname"
}
