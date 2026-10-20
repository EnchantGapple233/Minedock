# MineDock

[简体中文](README.md) | [English](README.en.md)

Minecraft Java 版的 Windows 桌面下载与安装工具，使用 C#、.NET 10 和 WPF 开发。

> 本项目完全源于许愿式编程，开发者在相关知识方面几乎是纯小白。十分感激各位大佬锐评指导。

## 下载

当前版本：**v0.1.2**。

- [Windows x64 便携版](https://github.com/EnchantGapple233/Minedock/releases/download/v0.1.2/MineDock-0.1.2-win-x64.zip)
- [版本说明与校验文件](https://github.com/EnchantGapple233/Minedock/releases/tag/v0.1.2)
- [全部版本](https://github.com/EnchantGapple233/Minedock/releases)

解压后运行 `MineDock.exe`，无需额外安装 .NET。程序面向 Windows 10 / 11 x64，目前只在 Windows 11 x64 实机验证。

## 功能

- 下载并安装 Minecraft 原版正式版，包含客户端、支持库、原生库包、资源文件和日志配置。
- 安装 Fabric 稳定版，并按游戏版本补齐 Fabric API。
- 从 Modrinth 搜索模组、整合包、光影和材质包，按游戏版本、加载器和当前栏目的内容分类等条件筛选。
- 解析模组的必需依赖，在安装确认前展示待补齐的名称、版本和文件。
- 安装模组和材质包；为 Fabric 光影生成包含 Iris 及声明依赖的安装计划。
- 下载整合包原始文件。
- 安装 Eclipse Temurin Windows x64 JRE / JDK，支持选择主版本和多版本独立目录。
- 关联 PCL 或 HMCL 后自动选择游戏目录，读取启动器的隔离与自定义运行目录设置，将资源写入对应位置。
- 下载缓存、大文件分段与断点续传、取消和重试、任务记录、哈希校验、安装失败回滚及中断恢复。
- 明暗主题、中文界面、栏目独立筛选和安装计划面板。
- 自定义背景图片，支持 PNG、JPEG、WebP、BMP，可调整背景强度、选择填充或适应，并恢复默认。
- Modrinth 当前页与总页数显示、对称的翻页按钮、指定页跳转，以及无效输入、加载与失败状态处理。
- 更清晰的页码、选中项上浮与柔和阴影、悬停和按下反馈，以及键盘焦点提示。

![MineDock 分页界面，v0.1.2](docs/images/pagination-dark.png)

## 使用

1. 点击「关联启动器」，选择实际的 PCL.exe、HMCL.exe 或 HMCL.jar。程序优先读取已配置的游戏目录，否则识别启动器旁的 `.minecraft`；未找到时可手动选择。
2. 安装游戏时选择 Minecraft 和 Fabric 版本；添加资源时选择已安装的游戏实例。
3. 搜索并筛选内容，创建安装计划，检查文件、依赖、下载大小和目标位置后确认执行。
4. 使用 PCL 或 HMCL 完成账号登录、游戏启动、隔离设置和已有模组管理。

向目标目录安装内容时，应先结束对应游戏，以及启动器中对同一目录执行的安装任务。MineDock 只读启动器配置，无法确认资源目录时会停止安装。更换启动器或游戏目录会清除原待安装计划。

Java 默认安装到 `%APPDATA%\.minecraft\runtime\MineDock`。安装后可在 PCL / HMCL 中搜索 Java，或手动导入对应版本的 `bin\java.exe`。程序不会修改 `PATH` 或 `JAVA_HOME`。

### 启动器兼容

保留已有 PCL 关联设置，支持 PCL 的便携目录写法。HMCL 同时支持旧版 `hmclversion.cfg` / `hmcl.json`，以及 3.16 系列的目录列表、游戏设置预设与实例运行目录。配置存在歧义、损坏或不支持的版本时会提示，保留「仅下载」。

默认按启动器所在文件夹解析相对路径；HMCL 使用环境变量指定配置位置时也会读取。通过快捷方式或脚本指定了其他工作目录或 JVM 配置路径的特殊启动方式，需要先核对实际目录。详情见[启动器兼容说明](docs/LAUNCHER-COMPATIBILITY.md)。

### 分页与跳转

搜索结果下方显示总项目数、当前页和总页数。使用左右箭头翻页；编辑页码后按 Enter 或点击跳转图标，按 Esc 可恢复当前页码。首页、末页、加载中和空结果时会禁用不可用操作。无效页码在分页区提示，网络失败时保留已有结果与页码。用新条件执行搜索时从第一页开始。

### 自定义背景

点击标题栏右上角的图片按钮选择背景。图片、强度和布局设置会自动保存；移动原图后仍可显示，恢复默认不会删除原图。支持最大 64 MB、4000 万像素的图片，缓存最长边不超过 2560 像素。明暗主题会分别控制背景显示强度，以保持文字可读。

![自定义背景设置，v0.1.1](docs/images/background-settings.png)

## 下载来源

| 内容 | 来源 |
| --- | --- |
| Minecraft 客户端、支持库和资源 | Mojang 官方 / BMCLAPI |
| Fabric | Fabric 官方接口与 Maven |
| 模组、整合包、光影、材质包 | Modrinth |
| Java | Eclipse Adoptium Temurin 官方分发 |

游戏来源可选择自动、官方或 BMCLAPI。自动模式对支持的 Mojang 下载域名优先使用 BMCLAPI，失败后回退官方；锁定来源时不会自动切换。其他服务保留各自的官方分发来源。

## 当前范围

当前是第一阶段版本。Forge、NeoForge、Quilt、QSL、Legacy Fabric、OptiFine 的完整安装，以及整合包 `.mrpack` 自动安装和游戏目录 ZIP 导入 / 解包，**尚未实现**。整合包页面目前支持搜索、筛选和原始文件下载。

不提供账号登录、游戏启动、模组启停或卸载。兼容性检查依据游戏版本、加载器、声明依赖及已知冲突，不能保证排除所有运行时冲突。

## 数据与校验

- 设置、历史、下载缓存、计划和事务日志位于 `%LOCALAPPDATA%\MineDock`。
- 下载完成并通过上游哈希校验后，才会提交文件到目标目录。
- 覆盖已有文件前会备份；失败、取消或异常退出后可回滚或恢复，并保护外部程序修改过的文件。
- 游戏、模组、整合包和 Java 不随本程序分发，由用户执行下载时从对应上游获取。

## 构建

在 Windows x64 安装 .NET 10 SDK，然后在仓库根目录执行：

```powershell
dotnet build MineDock.slnx -c Release
dotnet run --project .\MineDock.Checks\MineDock.Checks.csproj -c Release -- .\check-output
dotnet run --project .\MineDock.UiChecks\MineDock.UiChecks.csproj -c Release -- .\check-output-ui
dotnet publish .\MineDock.App\MineDock.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish
```

在线检查可在检查命令最后加 `--live`；`--live --install` 会下载约 1 GB，并在指定测试目录实际安装 Minecraft 1.21.1、Fabric、Fabric API 和 Java 21。`--live --mirror` 可单独检查 BMCLAPI。

`MineDock.UiChecks` 在独立应用数据目录运行实际 WPF 分页、文字边界、选中反馈和启动器关联检查；加 `--live` 可验证四个 Modrinth 栏目的真实翻页与末页访问，并生成明暗主题及最小窗口截图。

诊断截图：`MineDock.exe --capture <绝对输出目录>`。可设置 `MINEDOCK_DATA` 环境变量，将诊断数据与日常使用的设置分开。

## 验证状态

已完成 Minecraft 1.21.1、Fabric、Fabric API 和 Java 的实际安装验证，完整流程 65 项检查通过。另有包含重叠项目的 62 项本地及在线检查，不能与前一组相加。

v0.1.1 新增背景功能的 20 项专项检查及便携版的 5 项启动检查通过，本次未重复执行完整游戏下载。

v0.1.2 的 55 项分页与在线界面检查、92 项核心与启动器检查、20 项背景回归检查和 5 项便携程序启动检查通过，覆盖真实分页、八位页码文字边界、选中反馈、目录自动关联、HMCL 新旧配置和最小窗口布局。本次未重复执行约 1 GB 的完整游戏安装。

已检查官方 HMCL 3.16.4 EXE / JAR 的文件识别，配置兼容依据官方源码与本地配置样例验证。尚未完成真实 PCL / HMCL 启动游戏的实机验证，也未在 Windows 10 实机测试。BMCLAPI 已验证版本清单及一个资源文件，部分分发节点的证书错误导致完整镜像安装未获验证。

详见 [验证记录](VALIDATION.md) 和 [更新日志](CHANGELOG.md)。

## 许可

本项目使用 [GPL-3.0-or-later](LICENSE)。第三方组件许可与上游来源见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。下载内容保留各自的许可。
