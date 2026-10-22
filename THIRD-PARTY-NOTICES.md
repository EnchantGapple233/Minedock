# Third-Party Notices

## Lucide

Official Lucide SVG icons, pinned to release 0.468.0.

Source: https://github.com/lucide-icons/lucide/tree/0.468.0

The original license is included in `MineDock.App/Assets/Icons/LICENSE`. Lucide uses the ISC license, with applicable Feather-derived portions under the MIT license as documented upstream.

## PCL

MineDock 0.1.3 embeds the unmodified public stable PCL 2.99.0.0 EXE from the official archive at revision `6b9a50df911c9d68b69b24c979efba477fb14b67`. PCL uses its own **PCL Limited Distribution License**, which permits redistribution of unmodified copies subject to its terms. PCL is not covered by MineDock's GPL license. Earlier MineDock documentation incorrectly described the PCL repository as GPL-3.0; that description is corrected here.

No PCL snapshot, cracked build, or modified PCL source or assets are distributed. No fee is charged for accessing or installing PCL. MineDock does not accept the PCL first-run user agreement on a user's behalf.

The original repository license and distribution permission are embedded and viewable in the app. Exact versions, source URLs, and SHA-256 hashes are recorded in `MineDock.App/Assets/Launchers/SOURCES.md`.

Source: https://github.com/Meloong-Git/PCL

Distribution license and user agreement: https://shimo.im/docs/rGrd8pY8xWkt6ryW

## HMCL / HMCLauncher

MineDock embeds the unmodified official HMCL 3.16.4 Windows EXE, including its HMCLauncher 3.7.0.1 wrapper. Both projects use GPLv3 with additional terms under section 7: modified versions must be reasonably distinguished by name or version, and displayed copyright declarations must not be removed. Neither program is modified by MineDock.

The original GPL texts and upstream READMEs containing these additional terms are embedded and viewable in the app. The full corresponding HMCL and HMCLauncher source archives, build scripts, and provenance are included in the embedded `HMCL-3.16.4-corresponding-source.zip`, exportable from the license and source window without an Internet connection. The same source resource is included in MineDock's source distribution.

Since v0.1.4, MineDock also extracts the original HMCL JAR from the unmodified EXE and calls HMCLCore through `MineDockBridge.java` to install Forge, NeoForge, OptiFine, and LiteLoader. The bridge is identified as MineDock code under GPL-3.0-or-later; HMCL's engine, notices, and source remain unchanged. The extracted JAR SHA-256 is `98f0123f38e86a606aa583f74d21ae74128679e921ea7ec0c43ab26303767e9e`.

Sources: https://github.com/HMCL-dev/HMCL — https://github.com/HMCL-dev/HMCLauncher

## OpenJFX

The loader installation engine downloads the Windows JavaFX Base and Graphics 21.0.8 artifacts from Maven Central and verifies their upstream SHA-1 values. OpenJFX is licensed under GPLv2 with the Classpath Exception. These JARs are downloaded when needed rather than bundled in the MineDock EXE.

Source: https://github.com/openjdk/jfx

## SkiaSharp / Skia

SkiaSharp 4.153.1 decodes Modrinth project icons and custom background images, including WebP, into WPF images. The managed component and Windows native assets are distributed under MIT with their upstream third-party notices.

Source: https://github.com/mono/SkiaSharp

Original license files are included in `MineDock.App/Assets/Licenses/` and retained in the portable distribution.

## Microsoft .NET / WPF

The self-contained distribution contains Microsoft .NET and WPF runtime components. Their license and third-party notices remain in the published runtime distribution and are also embedded for viewing from the single-file application. Native and runtime components are automatically extracted by .NET when the single-file application starts.

Source: https://github.com/dotnet

## Download Services

- Mojang Minecraft metadata, game clients, libraries and assets: https://www.minecraft.net/
- BMCLAPI mirror: https://bmclapi2.bangbang93.com/ — https://bmclapidoc.bangbang93.com/
- Fabric metadata and Maven: https://fabricmc.net/
- Modrinth projects, versions, file hashes and content: https://modrinth.com/
- Eclipse Adoptium Temurin: https://adoptium.net/

Downloaded content retains its own license. Temurin archive license files are preserved during installation.
