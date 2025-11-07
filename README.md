# NetBoxPS

NetBoxPS is a build system for a self-generated PowerShell module that talks to the NetBox API. The repository holds the tooling
required to download an OpenAPI schema, patch it, generate a .NET SDK, and then reflect over that SDK to produce PowerShell
functions. The generated module lives entirely under source control–friendly folders so it can be rebuilt on demand.

> **Heads-up:** The checked-in code is not the PowerShell module itself. Running the build regenerates the module artifacts.

## Repository layout

- `src/NetBoxPS.Sdk/` – wraps OpenAPI Generator and houses the generated C# client. This project orchestrates fetching and
  patching the schema, then emits SDK sources during the build.
- `src/NetBoxPS.CodeGen/` – a .NET console app that will eventually reflect over the SDK and emit PowerShell-friendly wrappers.
  (Only scaffolding exists today.)
- `src/NetBoxPS.Module/` – a `Microsoft.Build.NoTargets` project that ties everything together and copies the generated module to
  `src/NetBoxPS.Module/module/` for inspection and packaging.

## Prerequisites

The toolchain has been aligned so the same workflow works on Windows, macOS, and Linux:

- [.NET SDK 8.0+](https://dotnet.microsoft.com/) – builds all three projects.
  - In Codespaces/Codex environments without the SDK preinstalled, run
    `scripts/install-dotnet.sh` to add the official Microsoft package feed and
    install `dotnet-sdk-8.0`. The script prints `dotnet --info` on success so
    you can verify the toolchain is ready.
- [PowerShell 7+ (`pwsh`)](https://learn.microsoft.com/powershell/) – runs the schema patch script. Override with
  `/p:PowerShellExe=powershell` if you must use Windows PowerShell 5.1.
- [Node.js + npm](https://nodejs.org/) – supplies `npx` for OpenAPI Generator.
- [`java`](https://openjdk.org/) (11 or newer) – required by OpenAPI Generator.
- [`curl`](https://curl.se/) – downloads the NetBox schema.

The SDK project checks for these tools up front and fails fast with actionable messages.

## Building

```bash
dotnet build NetBoxPS.sln
```

The solution build performs the following steps:

1. Restore and compile the OpenAPI-driven SDK in `src/NetBoxPS.Sdk/`.
2. Build the `NetBoxPS.CodeGen` console app (ready for future reflection-based generation).
3. Run `NetBoxPS.Module`, which invokes the code generator via `dotnet run` and stages the resulting module in
   `src/NetBoxPS.Module/module/` and `src/NetBoxPS.Module/bin/<Configuration>/module/`.

Because the build is MSBuild-based, you can target a specific configuration with `-c Release` and the process works from PowerShell
(`pwsh`), Bash, or any shell with the .NET CLI available.

### Incremental builds

The SDK project only re-fetches or regenerates the OpenAPI assets when inputs change. Likewise, the module project uses a stamped
output folder so repeated `dotnet build` runs stay fast.

### Cleaning up

Use `dotnet clean` to remove generated artifacts. This clears the schema cache, the generated SDK files, and the staged module.

## Customising the environment

- To run the schema patcher with Windows PowerShell, supply `/p:PowerShellExe=powershell` when building the SDK or solution.
- To inspect the generated SDK, look under `src/NetBoxPS.Sdk/Generated/` after a successful build.
- Module artifacts are staged beneath `src/NetBoxPS.Module/module/` and can be imported locally for experimentation once future
  code-generation logic is in place.
