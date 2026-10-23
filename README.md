# MineDock

[简体中文](README.md) | [English](README.en.md)

为 PCL / HMCL 用户准备的 Minecraft Java 版下载与安装工具：在一个中文窗口里选游戏、加载器和 Modrinth 资源，确认计划后安装到对应实例。

![MineDock 0.1.4 实际界面：Minecraft 1.21.1 与 Forge 版本选择](docs/images/v0.1.4-main.png)

## 下载

[![下载 MineDock.exe](https://img.shields.io/badge/下载-MineDock.exe-16834a?style=for-the-badge)](https://github.com/EnchantGapple233/Minedock/releases/latest/download/MineDock.exe)

[版本说明与 SHA-256 校验](https://github.com/EnchantGapple233/Minedock/releases/latest) · [全部版本与源码包](https://github.com/EnchantGapple233/Minedock/releases)

当前正式版 **v0.1.4**。Windows 10 / 11 x64，下载后直接运行单个 EXE，无需另装 .NET。目前只在 Windows 11 x64 实机验证。

## 三步上手

1. **关联启动器**：选择已有 PCL / HMCL；也可点击左下角下载图标，离线安装内置启动器并自动关联。
2. **选择内容**：安装游戏时选择 Minecraft 和加载器版本；添加模组、光影或材质包时选择已有实例。
3. **确认并安装**：检查安装计划里的依赖、大小与目标目录，完成后打开 PCL / HMCL 启动游戏。

## 和 PCL / HMCL 的关系

| 工具 | 负责什么 |
| --- | --- |
| MineDock | 下载游戏、加载器、Java 和 Modrinth 资源，解析依赖并安装到所选实例 |
| PCL / HMCL | 账号登录、游戏启动、实例隔离、已有模组的启停与卸载 |

MineDock 可以配合你现有的启动器使用，只读取其配置；也随程序附带原版 PCL / HMCL 及相应许可和源码导出入口。

## 适合做什么

- 安装原版游戏及 Forge、NeoForge、Fabric、Quilt、OptiFine、Legacy Fabric、LiteLoader，选择上游仍提供的兼容版本及预发布。
- 从 Modrinth 查找模组、光影、材质包和整合包，在安装前核对依赖与目标位置。
- 用中文界面管理下载和 Java，保留断点续传、失败恢复、自定义背景与自动更新。

**当前限制**：整合包只支持搜索和原始文件下载，尚未实现 `.mrpack` 自动安装或游戏目录 ZIP 导入；加载器兼容范围以各自上游为准。

## 帮助与参与

使用问题请到 [Discussions](https://github.com/EnchantGapple233/Minedock/discussions)，程序错误和功能建议请用 [Issue 模板](https://github.com/EnchantGapple233/Minedock/issues/new/choose)，安全问题请[私密报告](https://github.com/EnchantGapple233/Minedock/security/advisories/new)。

[使用指南](docs/USER-GUIDE.md) · [启动器兼容](docs/LAUNCHER-COMPATIBILITY.md) · [更新日志](CHANGELOG.md) · [构建与测试](docs/DEVELOPMENT.md) · [验证记录](VALIDATION.md) · [贡献指南](.github/CONTRIBUTING.md)

[![Windows Build](https://github.com/EnchantGapple233/Minedock/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/EnchantGapple233/Minedock/actions/workflows/build.yml)

## 关于与许可

> 本项目完全源于许愿式编程，开发者在相关知识方面几乎是纯小白。十分感激各位大佬锐评指导。

MineDock 源码使用 [GPL-3.0-or-later](LICENSE)。随附启动器和第三方组件保留各自许可，详见[第三方声明](THIRD-PARTY-NOTICES.md)及[来源记录](MineDock.App/Assets/Launchers/SOURCES.md)。本项目未获 Mojang / Microsoft 官方认可。
