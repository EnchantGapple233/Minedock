# Build and Validation

[Home](../README.en.md) | [简体中文](DEVELOPMENT.md)

MineDock uses C#, .NET 10, and WPF. Development requires Windows x64 and the .NET 10 SDK. CI pins the verified SDK 10.0.402. See the [user guide](USER-GUIDE.en.md) for usage, data locations, and caching.

## Complete Source

Use a complete repository checkout or the full source archive from a release. Original launchers, licenses, and HMCL corresponding source in `MineDock.App/Assets/Launchers` are required build resources. Do not retain only C# files or remove these resources to reduce size. See [provenance and hashes](../MineDock.App/Assets/Launchers/SOURCES.md).

## Build and Default Checks

Run from the repository root:

```powershell
dotnet build MineDock.slnx -c Release
dotnet run --project MineDock.Checks -c Release --no-build -- artifacts/checks/core
dotnet run --project MineDock.UiChecks -c Release --no-build -- artifacts/checks/ui
```

Default checks use controlled local fixtures and isolated directories rather than live Minecraft / Modrinth services. Core checks cover downloads, hashes, transactions, version parsing, update metadata, and launcher configuration. Full WPF checks include pagination, text bounds, hover states, caches, launchers, loaders, preloading, themes, refreshing, and backgrounds. A focused suite does not replace them.

Failures return a nonzero exit code. Each suite writes `results.json`; UI failures also write `failure.txt`. Background results are in `background-checks.json`, and screenshots remain in the UI output folder. Suites overlap; do not add their pass counts as unique features.

## Publish One EXE

```powershell
dotnet publish MineDock.App/MineDock.App.csproj -c Release -p:PublishProfile=SingleFile -o artifacts/single-file
./.github/scripts/verify-single-file.ps1 -PublishDirectory artifacts/single-file
```

The publish folder must contain only `MineDock.exe`. The verification script checks the folder, Windows x64 PE header, and project version, then writes `SHA256SUMS.txt` in the parent directory. Runtime caches, settings, and requested installations are not distribution dependencies.

The committed `packages.lock.json` describes the final single-file publish graph; a regular solution build changes it to a different graph. In a clean checkout or source archive, validate the locked publish **before a regular build**:

```powershell
dotnet publish MineDock.App/MineDock.App.csproj -c Release -p:PublishProfile=SingleFile -p:RestoreLockedMode=true -o artifacts/single-file
```

Do not apply `SingleFile` to the whole solution. Ordinary development does not require committing its changed lock graph. Regenerate and verify the final lock file with the publish profile before releasing. SDK, NuGet, and GitHub Actions upgrades must also validate locked restore; actions are pinned to full commit SHAs.

## Continuous Integration

[Windows Build](../.github/workflows/build.yml) runs on pushes to `main`, pull requests targeting `main`, and manual dispatch:

1. Publish the compressed, self-contained single EXE with locked restore on Windows 2025 x64 / SDK 10.0.402.
2. Verify the distribution folder, PE architecture, and version; generate SHA-256.
3. Build the solution and run core and full WPF default checks.
4. Save the EXE and checksum on success; preserve available reports and screenshots even on failure, for 14 days.

CI does not create releases, modify release assets, install every upstream loader build, log in, or launch Minecraft. The badge reflects actual GitHub run status; inspect results and logs on the repository's Actions page. CI artifacts represent their source commit; users should obtain stable releases from Releases.

## Live and Installation Checks

```powershell
dotnet run --project MineDock.Checks -c Release -- artifacts/checks/core-live --live
dotnet run --project MineDock.UiChecks -c Release -- artifacts/checks/ui-live --live
dotnet run --project MineDock.Checks -c Release -- artifacts/checks/mirror --live --mirror
dotnet run --project MineDock.Checks -c Release -- artifacts/checks/install --live --install
dotnet run --project MineDock.UiChecks -c Release -- artifacts/checks/loaders --loader-install
```

`--live` accesses real upstream APIs; UI live checks exercise pagination and final-page access across the four Modrinth sections. `--live --install` downloads about 1 GB and installs Minecraft 1.21.1, Fabric, Fabric API, and Java 21 in the test directory. `--live --mirror` also checks BMCLAPI.

`--loader-install` installs representative builds of all seven loaders plus historical Forge and NeoForge for Minecraft 1.20.1. It uses real clients and libraries, omits shared game assets, and discovers or downloads JDK 21+. These results do not imply that every historical build or actual game launch was verified.

When update replacement is affected, prepare the previous release in an isolated directory and run:

```powershell
dotnet run --project MineDock.UiChecks -c Release -- artifacts/checks/update --update-exe artifacts/single-file/MineDock.exe C:/isolated/previous/MineDock.exe
```

Never run fault injection, update recovery, or rollback checks against personal applications or game directories. Diagnostic screenshots use `MineDock.exe --capture <absolute-output-directory>`; set `MINEDOCK_DATA` to isolate diagnostic data. This mode still requests the game list.

## Before Releasing

Follow [maintenance rules](../AGENTS.md) and the [release checklist](RELEASE-CHECKLIST.md). Record current commands, evidence, dates, and limitations separately from the historical [validation record](../VALIDATION.md). Wait for maintainer confirmation before uploading, releasing, or closing related issues. Repository-only documentation and configuration changes do not rewrite released versions or the embedded changelog.
