# MineDock

[简体中文](README.md) | [English](README.en.md)

A Minecraft Java downloader and installer for PCL / HMCL users: choose a game, loader, and Modrinth content in one Chinese-language window, then review and install into the selected instance.

![Actual MineDock 0.1.4 interface: Minecraft 1.21.1 and Forge version selection](docs/images/v0.1.4-main.png)

## Download

[![Download MineDock.exe](https://img.shields.io/badge/Download-MineDock.exe-16834a?style=for-the-badge)](https://github.com/EnchantGapple233/Minedock/releases/latest/download/MineDock.exe)

[Release notes and SHA-256 checksums](https://github.com/EnchantGapple233/Minedock/releases/latest) · [All releases and source archives](https://github.com/EnchantGapple233/Minedock/releases)

Current stable release: **v0.1.4**. Windows 10 / 11 x64; run the single EXE with no separate .NET installation. Tested on Windows 11 x64 only so far.

## Start in Three Steps

1. **Associate a launcher**: select an existing PCL / HMCL, or use the bottom-left download icon to install and associate a bundled launcher offline.
2. **Choose content**: select Minecraft and loader versions for a new game, or select an existing instance for mods, shaders, and resource packs.
3. **Review and install**: check dependencies, sizes, and target paths in the installation plan, then open PCL / HMCL to launch the game.

## Working With PCL / HMCL

| Tool | Responsibilities |
| --- | --- |
| MineDock | Download games, loaders, Java, and Modrinth content; resolve dependencies and install into the selected instance |
| PCL / HMCL | Account login, game launching, instance isolation, and enabling, disabling, or removing existing mods |

MineDock reads the configuration of your existing launcher. Unmodified PCL / HMCL executables are also bundled with their licenses and corresponding-source export.

## What You Can Do

- Install vanilla Minecraft and compatible upstream releases or previews of Forge, NeoForge, Fabric, Quilt, OptiFine, Legacy Fabric, and LiteLoader.
- Find Modrinth mods, shaders, resource packs, and modpacks; review dependencies and destinations before installation.
- Manage downloads and Java in a Chinese interface, with resuming, failure recovery, custom backgrounds, and automatic updates.

**Current limits**: modpacks support search and original-file downloads only. Automatic `.mrpack` installation and game-directory ZIP imports are not implemented. Loader compatibility depends on each upstream project.

## Help and Contributions

Ask usage questions in [Discussions](https://github.com/EnchantGapple233/Minedock/discussions), report bugs or suggest features with the [issue forms](https://github.com/EnchantGapple233/Minedock/issues/new/choose), and [report vulnerabilities privately](https://github.com/EnchantGapple233/Minedock/security/advisories/new).

[User guide](docs/USER-GUIDE.en.md) · [Launcher compatibility](docs/LAUNCHER-COMPATIBILITY.md) · [Changelog](CHANGELOG.md) · [Build and test](docs/DEVELOPMENT.en.md) · [Validation record](VALIDATION.md) · [Contributing](.github/CONTRIBUTING.md)

[![Windows Build](https://github.com/EnchantGapple233/Minedock/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/EnchantGapple233/Minedock/actions/workflows/build.yml)

## About and License

> This project was created entirely through "wish-driven programming" with an AI coding assistant. The developer is almost a complete beginner in the relevant fields. Feedback and guidance are greatly appreciated.

MineDock's source uses [GPL-3.0-or-later](LICENSE). Bundled launchers and third-party components retain their licenses; see [third-party notices](THIRD-PARTY-NOTICES.md) and [provenance](MineDock.App/Assets/Launchers/SOURCES.md). This project is not endorsed by Mojang or Microsoft.
