# NetBoxPS Agent Guide

Welcome! This repository generates and packages a PowerShell module that wraps the NetBox API.

## Environment setup
- Use `scripts/install-dotnet.sh` to install the .NET SDK inside Codespaces/Codex if `dotnet --info` is unavailable.
- The apt packages needed for the script live in `scripts/apt-packages.txt` for reference.
- All projects target .NET 8; keep tooling aligned unless a version bump is coordinated across the solution.

## Repository layout
- `src/NetBoxPS.Sdk` hosts the OpenAPI generated client; do not check in generated sources under `Generated/` or `Schema/`.
- `src/NetBoxPS.CodeGen` contains the reflection-based scaffolding logic.
- `src/NetBoxPS.Module` holds the PowerShell module wrapper.
- `scripts/` contains automation helpers used by AI agents and contributors.

## Coding standards
- Favor deterministic, cross-platform tooling and avoid Windows-only APIs in shared code.
- Prefer `pwsh` for invoking PowerShell from build assets.
- Keep documentation up to date whenever scripts or workflows change.

## Testing
- At minimum run `dotnet build NetBoxPS.sln` after modifying source projects.
- For SDK changes, regenerate the client before committing if the OpenAPI schema changes.
