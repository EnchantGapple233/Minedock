# 发布回归清单

每次修改前阅读完整 `CHANGELOG.md`。以下是持续满足的历史能力，不是下一版本的新增功能；新版本应在已有基础上扩展。不能以只测新功能替代回归检查。

## 历史能力

| 版本 | 必须保留 | 检查位置 |
| --- | --- | --- |
| 0.1.0 | 原版 / Fabric / Fabric API、Modrinth 栏目与依赖、Java、PCL 隔离、安装计划和哈希、缓存 / 续传、取消 / 回滚 / 中断恢复、中文与明暗主题 | `MineDock.Checks`、`MineDock.UiChecks`；完整资源安装另用 `--live --install` |
| 0.1.1 | 四类背景格式、强度与布局、缓存恢复 / 原图移动、默认恢复、损坏和缺失缓存保护、主题与最小窗口 | `MineDock.UiChecks/BackgroundFeatureChecks.cs`，默认执行，独立输出 `background-checks.json` |
| 0.1.2 | 分页和键盘输入、错误 / 边界状态、异步结果保护、悬停和虚拟化宽度、PCL / HMCL 识别与只读配置 | `MineDock.UiChecks/Program.cs`、`MineDock.Checks/LauncherChecks.cs` |
| 0.1.3 | 启动入口、HMCL JAR / Java 识别、原样内置启动器与离线安装、文件保护和忙碌状态、许可与源码导出、悬停复位、四栏独立缓存 / 刷新 / 跨窗口恢复 | `LauncherFeatureChecks.cs`、`CatalogFeatureChecks.cs`、核心启动器检查 |
| 0.1.3 | 压缩、自带运行时的单 EXE，包含原生组件、启动器、图标、许可证与对应源码；无需附带 DLL 或另装 .NET | `SingleFile.pubxml`；在空目录验证最终 EXE，移除测试进程的 `DOTNET_ROOT` / `DOTNET_ROOT_X64` |
| 0.1.4 | 七类加载器和完整兼容列表、快照 / 历史版本、异步切换、真实安装处理 / 回滚、更新校验 / 替换 / 恢复、空闲预加载和请求复用 | `VersionFeatureChecks.cs`、`LoaderFeatureChecks.cs`、`LoaderInstallChecks.cs`、`UpdateSmokeChecks.cs`、`CatalogFeatureChecks.cs` |
| 0.1.4 | 更新菜单主题对比度、真实游戏空状态、切换来源重载、刷新保留筛选 / 页码 / 安装选择和忙碌保护 | `InterfaceFeatureChecks.cs`、`CatalogFeatureChecks.cs`、`LoaderFeatureChecks.cs` |

历史未实现的整合包自动安装、ZIP 解包和多加载器叠加不是已交付能力。不能把列表 / 计划检查等同于实际游戏启动，也不能声称为每个游戏提供不存在的加载器版本。

## 默认检查

从仓库根目录运行，测试使用独立数据目录；不要对真实用户的 `.minecraft` 或 EXE 运行恢复测试。

```powershell
dotnet build MineDock.slnx -c Release
dotnet run --project MineDock.Checks -c Release --no-build -- check-output-core --live
dotnet run --project MineDock.UiChecks -c Release --no-build -- check-output-ui --live
dotnet publish MineDock.App/MineDock.App.csproj -c Release -p:PublishProfile=SingleFile -o artifacts/single-file
```

普通界面测试构建与自带运行时发布使用不同依赖图，不要将 `SingleFile` 配置应用到整个解决方案。发布会为对应的 Windows x64 / 单文件依赖图更新锁定文件；源码包应保留这份最终锁定文件，并在解压后的源码上使用 `-p:PublishProfile=SingleFile -p:RestoreLockedMode=true` 再次验证发布。

实际安装与更新处理受到影响时，还应执行：

```powershell
dotnet run --project MineDock.Checks -c Release -- check-output-install --live --install
dotnet run --project MineDock.UiChecks -c Release -- check-output-loaders --loader-install
dotnet run --project MineDock.UiChecks -c Release -- check-output-update --update-exe artifacts/single-file/MineDock.exe C:/isolated/previous/MineDock.exe
```

加载器安装检查需可用 JDK；具体参数以检查程序为准。全资源安装耗时和下载量较大，不自动省略后声称已完成。若本次未重跑，保留原有证据、日期和限制。

## 交付门槛

- 发布目录只含一个 `MineDock.exe`，`EnableCompressionInSingleFile`、`SelfContained`、原生组件及全部内容内嵌均保持启用。文件版本与目标版本一致。
- 复制 EXE 到全新空目录，在独立数据及运行组件缓存目录中启动，确认不依赖开发 SDK 或旁边文件。运行时展开缓存、设置和用户选择的下载 / 安装目录是正常行为。
- 主界面、背景、许可与源码导出、启动器资源都可用；必要时查看明暗主题和最小窗口截图。
- 更新日志的本版本章节只含实际新增或修复；旧版本条目不重写。分发 / 使用说明可保留单 EXE 信息，但不再次当作新功能。
- 文档、最终 EXE、便携 ZIP 和源码 ZIP 一致；便携包仅含同一 EXE。重新计算 `SHA256SUMS.txt`，从源码 ZIP 解压后执行锁定还原及单文件发布。
- 在 `VALIDATION.md` 记录本次检查、证据和未覆盖范围。向维护者交付本地候选版，确认后才上传、发布和关闭 Issue。
