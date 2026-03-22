function Get-NBServerConfig {
    [CmdletBinding()]
    param()

    if ($script:NBConfig) {
        return $script:NBConfig
    }

    $configPath = Join-Path $HOME '.netboxrestconfig.json'
    if (Test-Path $configPath) {
        $config = Get-Content $configPath -Raw | ConvertFrom-Json
        $script:NBConfig = @{
            Host             = $config.Host
            APIToken         = $config.APIToken
            UseInsecureHttp  = [bool]$config.UseInsecureHttp
            IgnoreCertErrors = [bool]$config.IgnoreCertErrors
        }
        return $script:NBConfig
    }

    throw "No NetBox server configuration found. Run Initialize-NBServerConfig first."
}
