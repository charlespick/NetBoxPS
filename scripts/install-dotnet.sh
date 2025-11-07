#!/usr/bin/env bash
set -euo pipefail

# Installs the Microsoft .NET SDK using the official package feed.
# This script is intended for the Codespaces/Codex Debian/Ubuntu environment,
# but it should also work on any Debian-based distribution.

if [[ "${EUID}" -ne 0 ]]; then
  SUDO="sudo"
else
  SUDO=""
fi

# Ensure core dependencies for HTTPS apt repositories are installed.
$SUDO apt-get update
$SUDO apt-get install -y wget gpg apt-transport-https

# Register the Microsoft package repository if it is not already present.
if ! dpkg -s packages-microsoft-prod >/dev/null 2>&1; then
  temp_deb="$(mktemp)"
  wget -qO "$temp_deb" https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
  $SUDO dpkg -i "$temp_deb"
  rm -f "$temp_deb"
fi

$SUDO apt-get update
$SUDO apt-get install -y dotnet-sdk-8.0

# Print the installed SDK version for verification.
dotnet --info
