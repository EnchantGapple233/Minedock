# Bundled Launcher Sources

The official launcher executables are included without modification.
PCL and HMCL retain their own licenses. MineDock's GPL license does not
relicense PCL or its assets.

## PCL

- Public stable executable: 2.99.0.0.
- Official repository revision: `6b9a50df911c9d68b69b24c979efba477fb14b67`.
- Original archive: `最新正式版.zip`.
- Git blob SHA-1: `52843e0ba1c97a888630be8326fbc5a132157ccc`.
- Original archive SHA-256: `01877f06df84cbab376c286a633e76ef7fca0bb469e26df97f3b09b5abd503bc`.
- Executable SHA-256: `6b6e1260cb5a1d6a303855f1c60cab3d0bc1aebba7e40a964180188b271e7386`.
- Archive: https://github.com/Meloong-Git/PCL/blob/6b9a50df911c9d68b69b24c979efba477fb14b67/最新正式版.zip
- Distribution license: https://shimo.im/docs/rGrd8pY8xWkt6ryW#anchor-X0bo
- User agreement: https://shimo.im/docs/rGrd8pY8xWkt6ryW

The original archive contains one EXE. Only that EXE, renamed to PCL2.exe,
is embedded; its bytes remain unchanged. No snapshots, cracked builds,
PCL source code, or modified PCL assets are included. PCL is not sold.
The copied distribution permission and the upstream repository license
are included in this directory. Users read and decide on PCL's own
first-run agreement in PCL.

## HMCL

- Official release: 3.16.4.
- Source revision: `fbcc17472f08e877d60d6b715734e8054e9e61ee`.
- Windows EXE SHA-256: `632c2d8873f5a11dd2ffd8418564174995c6a248de5206668c6be114e943e372`.
- Source archive SHA-256: `246d54ad95130bc2eb7901cae518836e0fa17730011bdaebd85e5083bca6e499`.
- EXE: https://github.com/HMCL-dev/HMCL/releases/download/v3.16.4/HMCL-3.16.4.exe
- Source: https://api.github.com/repos/HMCL-dev/HMCL/zipball/v3.16.4
- Project: https://github.com/HMCL-dev/HMCL

The original EXE is renamed to HMCL.exe with unchanged bytes. Its Windows
wrapper is HMCLauncher 3.7.0.1. The upstream Gradle version catalog names
that version; the EXE prefix also matches the official 454144-byte
HMCLauncher executable.

## HMCLauncher

- Version: 3.7.0.1.
- Source revision: `da0e0c3b4f01fa0edbaf575d9631f093daf70cd1`.
- Original EXE SHA-256: `07476d13694bdba5ffcbb4786acf213c26eec650b78e710e0cd95c23d0e4147e`.
- Source archive SHA-256: `2acb684467b8313227565acf04b6b4ac5a35c51de2166e831467d5f3f8c00105`.
- Source: https://api.github.com/repos/HMCL-dev/HMCLauncher/zipball/da0e0c3b4f01fa0edbaf575d9631f093daf70cd1
- Project: https://github.com/HMCL-dev/HMCLauncher

## Offline Corresponding Source

`HMCL-3.16.4-corresponding-source.zip` is included as an embedded resource
and can be exported through MineDock's license and source window. It
contains the original full HMCL source archive, the full HMCLauncher
source archive, their GPL license texts, upstream READMEs with additional
terms, and this provenance record.

Extract the nested archives and follow `docs/Contributing.md` in HMCL
and `README.md` in HMCLauncher to build them. Their Gradle wrappers and
CMake build scripts are included. External build dependencies are
resolved by the upstream build tools. Neither launcher is recompiled or
modified by MineDock. Their original copyright declarations remain.
