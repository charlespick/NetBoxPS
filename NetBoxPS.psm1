# Load private functions
. "$PSScriptRoot\Private\Config.ps1"

# Load core helpers
. "$PSScriptRoot\Core\CoreHelpers.ps1"

# Load public API wrappers
Get-ChildItem "$PSScriptRoot\Public\*.ps1" | ForEach-Object {
    . $_.FullName
}
