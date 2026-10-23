# MineDock User Guide

[Home](../README.en.md) | [简体中文](USER-GUIDE.md)

This guide covers v0.1.4 features, usage, download sources, and limitations. See the [changelog](../CHANGELOG.md) for version history, the [development guide](DEVELOPMENT.en.md) for build and check commands, and the [validation record](../VALIDATION.md) for historical test evidence.

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

![MineDock interface, v0.1.4](images/v0.1.4-main.png)

## Usage

1. Associate an existing PCL.exe, HMCL.exe, or HMCL.jar, or use the bottom-left download icon to install and associate a bundled launcher. MineDock uses its configured game directory first, then looks for a neighboring `.minecraft` folder. Select a folder manually if none is found.
2. Choose Minecraft, a loader, and its version, or an existing game instance when adding content.
3. Search and filter content, create an installation plan, and review files, dependencies, download sizes, and destinations before confirming.
4. Use the bottom-left launch button to open PCL / HMCL, then use the launcher for account login, game launching, isolation settings, and management of existing mods.

Before installing into a target directory, close its running game and finish any launcher installation task using that directory. MineDock only reads launcher settings and stops resource installation when it cannot determine the destination. Changing the launcher or game folder clears the previous pending installation plan.

Java defaults to `%APPDATA%\.minecraft\runtime\MineDock`. After installation, let PCL / HMCL search for Java or manually select the relevant `bin\java.exe`. MineDock does not modify `PATH` or `JAVA_HOME`.

### Install and Open Launchers

Use the bottom-left download icon, choose an installation directory, and select PCL or HMCL. Both official EXEs are embedded, so installation works offline. MineDock creates or reuses a neighboring `.minecraft` folder. Identical existing files are reused; different existing launcher versions are preserved.

![Launcher installation window](images/v0.1.3-launcher-installer.png)

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

Relative paths use the launcher's folder as the workspace. HMCL environment variables for configuration locations are also read. Shortcuts or scripts that specify another working directory or JVM configuration paths require checking the actual directories first. See [launcher compatibility notes](LAUNCHER-COMPATIBILITY.md).

### Pagination

The footer shows the total project count, current page, and total pages. Use the arrow buttons to navigate, or edit the page number and press Enter or click the jump icon. Escape restores the current page number. Unavailable actions are disabled at page boundaries, during loading, and for empty results. Invalid input is reported beside the controls; failed requests preserve the displayed results and page. Searching with new criteria starts at page one.

### Custom Backgrounds

Use the image button at the top right of the title bar to select a background. The image, intensity, and layout settings are saved automatically and survive moving the original file. Restoring defaults does not delete the original. Images up to 64 MB and 40 megapixels are supported; cached images are limited to 2560 pixels on the longest edge. Light and dark themes adapt the displayed background strength to preserve text readability.

![Custom background settings, v0.1.1](images/background-settings.png)

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

## Network and Support

If the game list fails to load, check connectivity and the selected source, then use the title-bar refresh button. If filters return no results, check the version type, search terms, and upstream loader support. Modpacks can only be downloaded as original files; automatic `.mrpack` installation is not implemented.

Ask usage, compatibility, and connectivity questions in [Discussions](https://github.com/EnchantGapple233/Minedock/discussions). For reproducible application defects, use the [bug form](https://github.com/EnchantGapple233/Minedock/issues/new?template=bug_report.yml) and include MineDock / Windows / game / loader versions, the download source, and reproduction steps. Remove tokens, account details, personal paths, and private URLs from screenshots and logs.

Unavailable GitHub access may prevent automatic updates, but preserves the running version. Continue using the existing program and download from the [official Releases](https://github.com/EnchantGapple233/Minedock/releases) when connectivity returns. Third-party proxies are not official update sources and are not guaranteed to remain available.

## License

MineDock's source is licensed under [GPL-3.0-or-later](../LICENSE). The unmodified PCL payload retains the PCL Limited Distribution License. HMCL / HMCLauncher retain GPLv3 and their additional terms, and other components retain their original licenses. See [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) and [launcher provenance](../MineDock.App/Assets/Launchers/SOURCES.md) for exact versions, hashes, and sources.
