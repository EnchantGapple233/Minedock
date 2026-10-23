# 构建与验证

[返回首页](../README.md) | [English](DEVELOPMENT.en.md)

MineDock 使用 C#、.NET 10 和 WPF。开发需要 Windows x64 与 .NET 10 SDK；CI 固定使用已验证的 SDK 10.0.402。程序使用方式、数据目录和缓存机制见[使用指南](USER-GUIDE.md)。

## 完整源码

使用本仓库完整检出或 Release 中的完整源码包。`MineDock.App/Assets/Launchers` 内的原始启动器、许可及 HMCL 对应源码是构建所需资源，不能只保留 `.cs` 文件，也不能为了压缩体积移除它们。来源与哈希见[随附来源记录](../MineDock.App/Assets/Launchers/SOURCES.md)。

## 构建与默认回归

在仓库根目录运行：

```powershell
dotnet build MineDock.slnx -c Release
dotnet run --project MineDock.Checks -c Release --no-build -- artifacts/checks/core
dotnet run --project MineDock.UiChecks -c Release --no-build -- artifacts/checks/ui
```

默认检查使用本地可控数据与独立目录，不依赖 Minecraft / Modrinth 上游可用性。核心检查覆盖下载、哈希、事务、版本解析、更新元数据和启动器配置；完整 WPF 检查覆盖分页、文字边界、悬停、缓存、启动器、加载器、预加载、明暗主题、刷新和背景回归，不应只执行一个专项代替它们。

检查程序失败时返回非零退出码。结果在相应目录的 `results.json`；界面异常还会生成 `failure.txt`，背景专项结果为 `background-checks.json`，截图保存在界面检查目录。各场景可能重叠，不把不同套件通过数相加为独立功能数量。

## 发布单 EXE

```powershell
dotnet publish MineDock.App/MineDock.App.csproj -c Release -p:PublishProfile=SingleFile -o artifacts/single-file
./.github/scripts/verify-single-file.ps1 -PublishDirectory artifacts/single-file
```

发布目录必须只有 `MineDock.exe`。校验脚本核对目录、Windows x64 PE 标头及项目版本，并在其上一级生成 `SHA256SUMS.txt`。运行时缓存、用户设置及用户选择安装的文件不属于分发依赖。

已提交的 `packages.lock.json` 对应最终单 EXE 发布依赖图；普通解决方案构建会更新为另一依赖图。验证源码包的锁定发布时，应在干净检出或解压目录中**先于普通构建**运行：

```powershell
dotnet publish MineDock.App/MineDock.App.csproj -c Release -p:PublishProfile=SingleFile -p:RestoreLockedMode=true -o artifacts/single-file
```

不要将 `SingleFile` 配置应用到整个解决方案。普通开发构建不要求提交其产生的锁定文件变化；发版前由发布配置重新生成并核验最终锁定文件。升级 SDK、NuGet 包或 GitHub Actions 时同时验证锁定还原，Actions 固定到完整提交 SHA。

## 持续集成

[Windows Build](../.github/workflows/build.yml) 在推送 `main`、针对 `main` 的 Pull Request 和手动触发时运行：

1. Windows 2025 x64 / SDK 10.0.402 上锁定发布压缩、自带运行时的单 EXE。
2. 核对发布目录、PE 架构与版本并生成 SHA-256。
3. 构建完整解决方案，运行核心及完整 WPF 默认检查。
4. 成功后保存 EXE 和校验文件；检查报告与截图在失败时也尽量保存，保留 14 天。

CI 不创建 Release，不修改现有发布资产，不自动安装所有上游历史加载器，也不登录账号或启动 Minecraft。构建徽章展示 GitHub 的真实运行状态，具体结果与日志可在仓库 Actions 页面查看。CI 工件是当前提交的构建产物，用户下载正式版仍应使用 Releases。

## 联网与实际安装

```powershell
dotnet run --project MineDock.Checks -c Release -- artifacts/checks/core-live --live
dotnet run --project MineDock.UiChecks -c Release -- artifacts/checks/ui-live --live
dotnet run --project MineDock.Checks -c Release -- artifacts/checks/mirror --live --mirror
dotnet run --project MineDock.Checks -c Release -- artifacts/checks/install --live --install
dotnet run --project MineDock.UiChecks -c Release -- artifacts/checks/loaders --loader-install
```

`--live` 访问真实上游接口；界面联网检查会验证四个 Modrinth 栏目的翻页与末页访问。`--live --install` 下载约 1 GB，在指定测试目录安装 Minecraft 1.21.1、Fabric、Fabric API 和 Java 21。`--live --mirror` 另检查 BMCLAPI。

`--loader-install` 实际安装七类加载器的代表版本，另含旧 Forge 和 NeoForge 1.20.1。它下载真实客户端与支持库，但省略共享游戏资源；需要可发现的 JDK 21+，否则自动下载。这些都不能等同于验证所有历史构建或实际启动游戏。

更新替换受影响时，在隔离目录中准备上一正式版，再运行：

```powershell
dotnet run --project MineDock.UiChecks -c Release -- artifacts/checks/update --update-exe artifacts/single-file/MineDock.exe C:/isolated/previous/MineDock.exe
```

禁止用真实用户程序或游戏目录做更新恢复、故障注入和回滚测试。诊断截图命令是 `MineDock.exe --capture <绝对输出目录>`；可设置 `MINEDOCK_DATA` 隔离诊断数据。诊断截图模式仍会请求游戏列表。

## 发布前

按[维护规则](../AGENTS.md)与[发布回归清单](RELEASE-CHECKLIST.md)检查，记录本次实测、证据、日期和限制；历史结果保留在[验证记录](../VALIDATION.md)。上传、发布和关闭关联 Issue 等待维护者确认。只调整仓库文档和配置时，不改写已发布版本或嵌入的更新日志。
