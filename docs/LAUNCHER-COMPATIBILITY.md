# 启动器兼容 / Launcher Compatibility

适用版本 / MineDock version: **0.1.3**

## 中文

在左下角点击「关联启动器」，选择 PCL.exe、HMCL.exe 或 HMCL.jar。识别通过后，目标目录、实例列表和资源写入目录立即更新，设置自动保存。旧版 MineDock 保存的 PCL 关联仍然有效。

关联后点击左下角的「启动 PCL / HMCL」即可打开启动器。EXE 以其所在目录作为工作目录，不附加启动参数；HMCL JAR 使用 Java 17 或更高版本，通过独立参数传入完整 JAR 路径，支持空格和中文。找不到可用 Java 时会提示选择 `javaw.exe` / `java.exe`，也可先在 Java 栏目安装运行环境。

旁边的下载图标打开启动器安装窗口，可将内置、未修改的 PCL 2.99.0.0 或 HMCL 3.16.4 官方 EXE 离线安装到选定目录。MineDock 校验大小、SHA-256 和文件身份；相同文件直接复用，不同的同名文件保留并提示选择其他目录。安装会创建或复用旁边的 `.minecraft`，随后按下表的配置规则自动关联；已有启动器的显式游戏目录设置优先。Java、JavaFX、账号登录和游戏仍由相应的安装流程或启动器处理，内置 EXE 不代表首次运行启动器也无需联网。

| 启动器 | 游戏目录自动选择 | 资源写入目录 |
| --- | --- | --- |
| PCL | `PCL/Setup.ini` 中的 `LaunchFolderSelect`，支持 `$` 便携路径；没有记录时查找启动器旁的 `.minecraft` | 版本隔离设置优先，再读取全局隔离策略 |
| HMCL 旧版配置 | `hmcl.json`、`.hmcl.json` 或 `.hmcl/settings.json` 中当前选中的目录配置 | `hmclversion.cfg` 的独立设置或对应目录的全局设置；支持根目录、版本目录和自定义目录 |
| HMCL 3.16 系列配置 | `.hmcl/config/game-directories.json`、共享用户目录列表及选中目录 ID | 实例的 `.hmcl/config/instance-game-settings.json`、父预设与默认预设中的实际运行目录 |

HMCL 的 `modpack.json` 标记实例使用版本目录。新版 HMCL 的「新安装默认隔离」用于 HMCL 自身安装流程，MineDock 读取已有实例的实际运行设置，不擅自套用该安装选项。

未找到游戏目录时会打开手动选择窗口。配置损坏、目录选择存在歧义或配置主版本不支持时会提示，不猜测资源目标。可以继续使用「仅下载」。更换启动器或手动改变游戏目录会清除旧的待安装计划；运行安装任务期间不能更换。

MineDock 只读取启动器配置，不迁移或修改它们。启动器打开后自行管理其配置。通过启动器完成登录、游戏启动、隔离设置与模组管理。在启动器更改设置后保存并点击 MineDock 的刷新按钮。

默认以启动器所在文件夹作为相对路径基准，兼容通常的同目录便携用法。支持读取 `HMCL_LOCAL_HOME`、`HMCL_USER_HOME` 环境变量和 Windows 的默认 HMCL 用户目录。通过快捷方式或脚本指定其他工作目录、通过 JVM `-Dhmcl.*` 指定其他配置位置的情况，无法单凭 EXE / JAR 文件还原，需要核对实际目录；不承诺覆盖所有第三方定制启动器。

验证包括本地配置样例、官方 HMCL 3.16.4 EXE / JAR 识别、离线安装和实际 WPF 关联流程，并使用生产代码的进程启动参数实际打开了内置 PCL 与 HMCL 的窗口。**尚未完成真实 PCL / HMCL 启动游戏的验证**。启动器原件、许可及 HMCL / HMCLauncher 对应源码已内置，游戏和 Java 未打包分发。许可与来源见 `MineDock.App/Assets/Launchers/SOURCES.md`。

## English

Use the launcher association button at the bottom left to select PCL.exe, HMCL.exe, or HMCL.jar. After recognition, MineDock updates and saves the game root, instance list, and content destination. Previous PCL associations remain compatible.

The launch button opens the associated launcher. EXEs use their own directory as the working directory without extra arguments. HMCL JARs use Java 17 or later, passing the complete JAR path as a separate argument, including spaces and Unicode. If no compatible Java is found, select `javaw.exe` / `java.exe` or install a runtime from the Java section first.

The adjacent download icon opens the launcher installer. It installs the bundled, unmodified official PCL 2.99.0.0 or HMCL 3.16.4 EXE offline to a chosen folder, verifies size, SHA-256, and identity, reuses an identical file, and preserves a different existing file. It creates or reuses a neighboring `.minecraft`, then associates the launcher using the rules below; an existing launcher's explicit game-directory configuration takes precedence. Java, JavaFX, authentication, and game installation remain separate, so installing the bundled EXE offline does not guarantee an offline first launch.

- PCL: read the selected root from `PCL/Setup.ini`, including `$` portable paths; otherwise detect a neighboring `.minecraft`. Resolve instance isolation before the global policy.
- Legacy HMCL: read the selected directory profile from `hmcl.json`, `.hmcl.json`, or `.hmcl/settings.json`. Respect local `hmclversion.cfg` settings, inheritance, and root / version / custom run directories.
- HMCL 3.16 format: read local and shared directory lists, the selected directory, instance settings, parent presets, and the default preset. Use the actual run directory, not HMCL's default-isolation hint for its own installer.
- HMCL-managed modpacks identified by `modpack.json` use their version directory.

Missing game directories prompt manual selection. Ambiguous, corrupt, or unsupported configuration is reported instead of guessing a destination. Downloading without installation remains available. Switching launchers or game roots clears the old pending plan; running installations prevent reassociation.

MineDock reads launcher configuration without modification or migration. Once opened, the launcher manages its own configuration. Use the launcher for authentication, game launching, isolation, and mod management. Save changes in the launcher and refresh MineDock afterward.

Relative paths use the launcher's folder. `HMCL_LOCAL_HOME`, `HMCL_USER_HOME`, and the default Windows HMCL user directory are supported. A shortcut or script may supply another working directory or JVM `-Dhmcl.*` options; these cannot be reconstructed from the launcher file alone and require checking the actual paths. Compatibility with every third-party launcher fork is not claimed.

Validation covers configuration fixtures, official HMCL 3.16.4 EXE / JAR recognition, offline installation, and actual WPF association workflows. Both bundled launchers were opened using the production process-start configuration. **Launching the game through PCL / HMCL has not been verified.** Original launchers, licenses, and corresponding HMCL / HMCLauncher source are bundled; game and Java files are not. See `MineDock.App/Assets/Launchers/SOURCES.md` for provenance and terms.

## 上游参考 / Upstream References

- [PCL: selected root and portable paths](https://github.com/Meloong-Git/PCL/blob/main/Plain%20Craft%20Launcher%202/Modules/Minecraft/ModMinecraft.vb)
- [HMCL 3.6.20: profile configuration](https://github.com/HMCL-dev/HMCL/blob/v3.6.20/HMCL/src/main/java/org/jackhuang/hmcl/setting/Profile.java)
- [HMCL 3.6.20: run-directory resolution](https://github.com/HMCL-dev/HMCL/blob/v3.6.20/HMCL/src/main/java/org/jackhuang/hmcl/game/HMCLGameRepository.java)
- [HMCL 3.16.4: configuration locations](https://github.com/HMCL-dev/HMCL/blob/v3.16.4/HMCL/src/main/java/org/jackhuang/hmcl/setting/SettingsManager.java)
- [HMCL 3.16.4: instance settings and actual run directories](https://github.com/HMCL-dev/HMCL/blob/v3.16.4/HMCL/src/main/java/org/jackhuang/hmcl/game/HMCLGameRepository.java)
- [HMCL 3.16.4: legacy settings migration](https://github.com/HMCL-dev/HMCL/blob/v3.16.4/HMCL/src/main/java/org/jackhuang/hmcl/setting/LegacyGameSettingsMigrator.java)
