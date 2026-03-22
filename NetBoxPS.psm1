# Dot-source all private functions
Get-ChildItem -Path "$PSScriptRoot\Private\*.ps1" -ErrorAction SilentlyContinue | ForEach-Object {
    . $_.FullName
}

# Dot-source all public functions
Get-ChildItem -Path "$PSScriptRoot\Public\*.ps1" -ErrorAction SilentlyContinue | ForEach-Object {
    . $_.FullName
}
