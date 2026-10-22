# MineDock

[简体中文](README.md) | [English](README.en.md)

A Windows desktop downloader and installer for Minecraft Java Edition, built with C#, .NET 10, and WPF.

> This project was created entirely through "wish-driven programming" with an AI coding assistant. The developer is almost a complete beginner in the relevant fields. Feedback and guidance are greatly appreciated.

## Downloads

Current version: **v0.1.4**, available as a self-contained Windows x64 single EXE.

- [Download MineDock.exe directly](https://github.com/EnchantGapple233/Minedock/releases/download/v0.1.4/MineDock.exe)
- [Windows x64 portable ZIP](https://github.com/EnchantGapple233/Minedock/releases/download/v0.1.4/MineDock-0.1.4-win-x64.zip)
- [Complete source ZIP](https://github.com/EnchantGapple233/Minedock/releases/download/v0.1.4/MineDock-0.1.4-source.zip)
- [Release notes and checksums](https://github.com/EnchantGapple233/Minedock/releases/tag/v0.1.4)
- [All releases](https://github.com/EnchantGapple233/Minedock/releases)

Run `MineDock.exe` directly, or extract the portable ZIP and run the same file. The .NET runtime is included. The application targets Windows 10 / 11 x64; it has only been tested on Windows 11 x64 so far.

### New in 0.1.4

- Install Forge, NeoForge, Fabric, Quilt, OptiFine, Legacy Fabric, and LiteLoader, with complete compatible release lists and previews.
- Filter Minecraft releases, snapshots, and historical Alpha / Beta versions, with protection against stale selection responses.
- Check GitHub Releases automatically, verify and replace the EXE, restart when idle, and recover failures. Include manual checking and an opt-out.
- Theme the update menu, distinguish game-list failures from empty filters, and reload on source changes. Refresh the interface from the title bar while preserving filters, pagination, and installation choices.
- Preload common resource lists, tags, and icons in the background after the initial view appears.

See the [changelog](CHANGELOG.md) for the complete list.

## Features

- Install Minecraft Java releases, snapshots, and historical versions, including the client, libraries, native library archives, assets, and logging configuration.
- Install compatible versions of seven loader families, including previews, with optional matching Fabric API installation.
- Search Modrinth for mods, modpacks, shaders, and resource packs, with game version, loader, and category filters appropriate to each section.
- Resolve required mod dependencies and show their names, versions, and files before installation.
- Install mods and resource packs; generate Fabric shader installation plans that include Iris and its declared dependencies.
- Download original modpack files.
- Install official Eclipse Temurin Windows x64 JRE / JDK builds into separate version directories.
- Associate PCL or HMCL, automatically select its game directory, and read isolation and custom run-directory settings to select the content destination.
- Download caching, segmented transfers and resuming, cancellation and retries, task history, hash verification, rollback, and recovery after interruption.
- Light and dark themes, a Chinese-language interface, independent section filters, and an installation plan panel.
- Custom PNG, JPEG, WebP, or BMP backgrounds, with intensity, fill / fit layout, and restore-default controls.
- Current and total Modrinth page counts, matching navigation buttons, direct page jumps, and invalid-input, loading, and failure handling.
- Larger page numbers, hover elevation and soft shadows, a smooth return on pointer leave, selected colors, pressed feedback, and keyboard focus indicators.
- Separate result caches for each section, preserving filters, pagination, selection, file-version choice, and scroll position when switching back.
- Open the associated PCL / HMCL directly, including EXE and HMCL JAR support.
- Install bundled, unmodified official PCL 2.99.0.0 and HMCL 3.16.4 executables offline and associate them automatically. A self-contained single EXE is available.
- View component licenses and export the included HMCL / HMCLauncher corresponding source without an Internet connection.

![MineDock interface, v0.1.4](docs/images/v0.1.4-main.png)

## Usage

1. Associate an existing PCL.exe, HMCL.exe, or HMCL.jar, or use the bottom-left download icon to install and associate a bundled launcher. MineDock uses its configured game directory first, then looks for a neighboring `.minecraft` folder. Select a folder manually if none is found.
2. Choose Minecraft, a loader, and its version, or an existing game instance when adding content.
3. Search and filter content, create an installation plan, and review files, dependencies, download sizes, and destinations before confirming.
4. Use the bottom-left launch button to open PCL / HMCL, then use the launcher for account login, game launching, isolation settings, and management of existing mods.

Before installing into a target directory, close its running game and finish any launcher installation task using that directory. MineDock only reads launcher settings and stops resource installation when it cannot determine the destination. Changing the launcher or game folder clears the previous pending installation plan.

Java defaults to `%APPDATA%\.minecraft\runtime\MineDock`. After installation, let PCL / HMCL search for Java or manually select the relevant `bin\java.exe`. MineDock does not modify `PATH` or `JAVA_HOME`.

### Install and Open Launchers

Use the bottom-left download icon, choose an installation directory, and select PCL or HMCL. Both official EXEs are embedded, so installation works offline. MineDock creates or reuses a neighboring `.minecraft` folder. Identical existing files are reused; different existing launcher versions are preserved.

![Launcher installation window](docs/images/v0.1.3-launcher-installer.png)

Installation automatically associates the launcher. Existing launchers can still be selected through the association button. Launching uses the executable's own directory as its working directory. HMCL JAR files use Java 17 or later; MineDock can find its installed runtime or prompt for `javaw.exe` / `java.exe`.

Offline installation means writing the embedded EXE to the selected directory. HMCL may still obtain Java or JavaFX on its first run; authentication and game downloads may also require an Internet connection.

### Single EXE

Since v0.1.3, the release distribution needs only `MineDock.exe`; installing .NET separately is unnecessary. On first launch, .NET extracts runtime and native components into its temporary cache. Settings and downloads retain their application-data locations. Installed launchers and `.minecraft` remain in the chosen directory.

The shield icon opens license notices and the offline HMCL source export. A single-file distribution still creates runtime caches and requested installation files.

### Loaders and Versions

Choose Forge, NeoForge, Fabric, Quilt, OptiFine, Legacy Fabric, or LiteLoader. The version list retains compatible releases and previews returned upstream and defaults to the newest stable release. Supported game versions depend on each project; unavailable combinations cannot be installed.

Fabric, Quilt, and Legacy Fabric use their Meta profiles. The remaining loaders use the bundled, unmodified HMCL 3.16.4 engine in a separate staging directory before transactional installation. The engine requires JDK 21+ and OpenJFX, or a higher Java version required by the game. The plan includes a suitable Temurin JDK if none is found. Installer and processing dependencies may be downloaded during execution and are additional to the initial plan size. Logs are under `%LOCALAPPDATA%\MineDock\loader-logs`.

Fabric API remains optional. Install QSL / QFAPI through the mods section for an installed instance. Combining multiple primary loaders is outside this release.

LiteLoader snapshots resolve actual builds from official Maven metadata and verify SHA-1, avoiding an unavailable mirror node. Historical ASM libraries use Maven Central.

### Automatic Updates

Use the title-bar update icon to check manually or disable automatic updates. The preference persists. By default MineDock checks the latest stable GitHub Release from `EnchantGapple233/Minedock` on startup and every hour, excludes previews, and never downgrades. The single EXE must pass size and GitHub SHA-256 checks, with `SHA256SUMS.txt` as a legacy fallback.

After downloading, the status remains visible for 10 seconds. Replacement and restart wait for active tasks, pending installation plans, and child windows. A separate helper retains the original EXE and waits for the new window to become ready, restoring the previous program on failure. Network errors, rate limits, missing write access, and invalid hashes preserve the running version. Update data and results are under `%LOCALAPPDATA%\MineDock\updates`. Automatic replacement requires the release single EXE; development builds only check and download.

### Launcher Compatibility

Existing PCL associations remain compatible, including portable directory paths. HMCL integration supports legacy `hmclversion.cfg` / `hmcl.json` settings and the directory lists, game settings presets, and instance run directories introduced in the 3.16 series. Ambiguous, corrupted, or unsupported configuration is reported; downloading files without installing remains available.

Relative paths use the launcher's folder as the workspace. HMCL environment variables for configuration locations are also read. Shortcuts or scripts that specify another working directory or JVM configuration paths require checking the actual directories first. See [launcher compatibility notes](docs/LAUNCHER-COMPATIBILITY.md).

### Pagination

The footer shows the total project count, current page, and total pages. Use the arrow buttons to navigate, or edit the page number and press Enter or click the jump icon. Escape restores the current page number. Unavailable actions are disabled at page boundaries, during loading, and for empty results. Invalid input is reported beside the controls; failed requests preserve the displayed results and page. Searching with new criteria starts at page one.

### Custom Backgrounds

Use the image button at the top right of the title bar to select a background. The image, intensity, and layout settings are saved automatically and survive moving the original file. Restoring defaults does not delete the original. Images up to 64 MB and 40 megapixels are supported; cached images are limited to 2560 pixels on the longest edge. Light and dark themes adapt the displayed background strength to preserve text readability.

![Custom background settings, v0.1.1](docs/images/background-settings.png)

### List Caching

Mods, modpacks, shaders, and resource packs cache their successful lists separately. Returning restores the previous view. The current session reuses recently viewed version lists, and loaded icons remain visible across section changes.

Lists are also saved to `%LOCALAPPDATA%\MineDock\catalog-cache` for 24 hours, including across restarts. A new window fetches file versions when a project is first selected. Use the section's refresh button or submit a new search to request current results. Refresh retains the current page; a new search starts at page one. Changing the target game or loader reapplies its filters. Corrupt, expired, or unwritable caches do not prevent normal searches.

After the first view appears, MineDock preloads tags, the first page of each resource section, and up to six icons per section. Fresh caches and in-flight requests are reused. Warmup leaves the current view and filters untouched, pauses subsequent requests during installation or plan preparation, and cancels on close. Failed requests remain retryable. It prepares browsing data without downloading game payloads, mod files, or Java.

## Download Sources

| Content | Source |
| --- | --- |
| Minecraft client, libraries, and assets | Mojang official servers / BMCLAPI |
| Fabric | Official Fabric metadata and Maven |
| Quilt / Legacy Fabric | Their official Meta and Maven services |
| Forge / NeoForge | BMCLAPI indexes; HMCL downloads installers and dependencies according to the source setting |
| OptiFine / LiteLoader | BMCLAPI and HMCL-compatible distribution services |
| Mods, modpacks, shaders, and resource packs | Modrinth |
| Java | Official Eclipse Adoptium Temurin distribution |
| MineDock updates | This repository's GitHub Releases |

The game source can be automatic, official, or BMCLAPI. Automatic mode tries BMCLAPI first for supported Mojang domains and falls back to official servers on failure. A locked source does not switch automatically. Other services retain their own official distribution sources.

## Current Scope

Automatic `.mrpack` installation and game-directory ZIP import / extraction are **not implemented yet**. The modpack section supports search, filtering, and downloading original files. Loader support covers the seven families above; it does not guarantee every historical artifact remains available upstream or arbitrary loader combinations are compatible.

Account login, the actual game process, mod enabling / disabling, and uninstalling remain the launcher's responsibility. MineDock provides a way to open the launcher. Compatibility checks use game versions, loaders, declared dependencies, and known conflicts; they cannot rule out every runtime conflict.

## Data and Verification

- Settings, history, caches, installation plans, and transaction journals are stored in `%LOCALAPPDATA%\MineDock`.
- Remote files in MineDock's plan pass upstream hashes before commit. HMCL validates loader dependencies under its own engine rules; generated files receive local SHA-256 checks before commit to detect staging changes. Local checks are not upstream signatures.
- Existing files are backed up before replacement. Failed or cancelled operations can roll back; interrupted transactions can recover while protecting files changed by other programs.
- Minecraft, mods, modpacks, and Java are downloaded from their upstream providers when requested. The bundled PCL / HMCL programs are independent, unmodified launchers with their own licenses.

## Building

Install the .NET 10 SDK on Windows x64, then run from the repository root:

```powershell
dotnet build MineDock.slnx -c Release
dotnet run --project .\MineDock.Checks\MineDock.Checks.csproj -c Release -- .\check-output
dotnet run --project .\MineDock.UiChecks\MineDock.UiChecks.csproj -c Release -- .\check-output-ui
dotnet publish .\MineDock.App\MineDock.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish
dotnet publish .\MineDock.App\MineDock.App.csproj -c Release -p:PublishProfile=SingleFile -o .\artifacts\single-file
```

Append `--live` to the check command to exercise upstream APIs. `--live --install` downloads approximately 1 GB and installs Minecraft 1.21.1, Fabric, Fabric API, and Java 21 into the specified test directory. `--live --mirror` checks BMCLAPI separately.

`MineDock.UiChecks` runs actual WPF pagination, text bounds, hover and selection feedback, list caching, and launcher association / installation checks with isolated application data. Append `--live` to verify real pagination across all four Modrinth sections and access to the final page, and to capture light / dark and minimum-window screenshots.

`dotnet run --project .\MineDock.UiChecks -c Release -- .\check-output-loaders --loader-install` installs representative versions of all seven loaders plus legacy Forge and NeoForge for Minecraft 1.20.1. It uses real clients and libraries while omitting shared game assets. The engine discovers a JDK 21+ or downloads one.

Diagnostic screenshots: `MineDock.exe --capture <absolute-output-directory>`. Set `MINEDOCK_DATA` to keep diagnostic data separate from your normal settings.

## Validation Status

Actual installation of Minecraft 1.21.1, Fabric, Fabric API, and Java has been verified, with 65 checks passing in the full workflow. A separate set of 62 local and online checks overlaps with that set and must not be added to it as a total.

The v0.1.1 background update passed 20 focused checks and 5 portable startup checks. The full game download was not repeated for this update.

The v0.1.2 update passed 55 pagination and online UI checks, 92 core and launcher checks, 20 background regression checks, and 5 portable startup checks. Coverage includes real pagination, eight-digit page text bounds, selection feedback, automatic directory association, legacy and current HMCL settings, and minimum-window layout. The approximately 1 GB full game installation was not repeated.

The v0.1.3 update passed 141 UI / launcher checks, including hover elevation, pointer-leave restoration, cache reuse, cross-window restoration, refresh, corrupt / expired caches, and asynchronous switching. The 92 core checks and 20 background regression checks also passed. The final single EXE was checked in an isolated directory with live lists, icons, page restoration, and normal exit. The initial 0.1.3 candidate opened both bundled launchers, the installer, and license notices, and exported corresponding HMCL source offline. The full game installation was not repeated.

Official HMCL 3.16.4 EXE / JAR identity detection has been checked. Configuration compatibility was verified against official source code and local configuration fixtures. Launching the installed game through PCL / HMCL and running on Windows 10 have not been verified. BMCLAPI checks retrieved the version manifest and one asset successfully; certificate failures on some distribution nodes prevented validation of a complete mirror installation.

See [validation notes](VALIDATION.md), currently in Chinese, and the [changelog](CHANGELOG.md).

## License

MineDock's source is licensed under [GPL-3.0-or-later](LICENSE). The unmodified PCL payload retains the PCL Limited Distribution License. HMCL / HMCLauncher retain GPLv3 and their additional terms, and other components retain their original licenses. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and [launcher provenance](MineDock.App/Assets/Launchers/SOURCES.md) for exact versions, hashes, and sources.
