# Changelog / 更新日志

## [0.1.4] - 2026-10-22

### 中文

- 修复游戏安装页只提供 Fabric 的限制，加入 Forge、NeoForge、Quilt、OptiFine、Legacy Fabric 与 LiteLoader。
- 按 Minecraft 版本联网获取兼容加载器，展示全部返回版本及预发布标识，默认选择最新稳定版；区分无兼容版本与请求失败。
- Minecraft 列表加入快照及历史 Alpha / Beta，可按版本类型筛选；保留正式版默认视图。
- 切换游戏或加载器立即清空旧选择并取消旧请求，迟到响应不能覆盖当前列表，加载中或没有兼容版本时禁止创建安装计划。
- Fabric、Quilt、Legacy Fabric 使用对应 Meta 启动配置；Forge、NeoForge、OptiFine、LiteLoader 使用随附、未经修改的 HMCL 引擎完成安装处理及支持库补齐。
- 修复历史加载器的原生库分类及失效 ASM 地址；LiteLoader 快照从官方 Maven 元数据解析实际构建，下载前获取上游校验值。
- 加载器在独立临时目录处理，生成文件经 SHA-256 校验后纳入现有事务；取消或失败回滚，并保存安装日志。复用满足要求的本机 JDK，缺少时在计划中加入 Temurin JDK 21 或游戏要求的更高版本。
- 从本仓库正式 GitHub Releases 自动检查新版本，核对单 EXE 大小与 SHA-256，空闲后自动替换并重启；忙碌、待确认计划及子窗口打开期间延后。
- 更新使用独立助手、原程序备份和新窗口就绪确认；替换或启动失败恢复原版本，保护被其他程序修改的文件。支持手动检查及关闭自动更新。
- 修复自动更新菜单的主题配色，确保明暗主题下状态文字、操作项和勾选标记可读；打开更新菜单时延后程序替换。
- 区分游戏版本加载中、联网失败和筛选为空；切换来源自动重载，取消旧来源请求，刷新失败保留已有列表，筛选为空时可恢复全部版本。
- 增加标题栏界面刷新按钮，重新扫描本地版本并刷新当前栏目；保留资源筛选和页码、游戏与加载器选择及自定义名称，忙碌时禁用重复刷新。
- 首屏显示后先在后台预加载资源分类及四个 Modrinth 栏目的第一页，再加载每栏最多 6 个图标，避免慢图标拖延其他列表；复用有效缓存和进行中的请求，首次切换直接读取，不改变当前页面或下载游戏大文件。
- 增加完整版本列表、预发布、异步切换、安装、更新校验和恢复检查。

### English

- Remove the Fabric-only restriction and add Forge, NeoForge, Quilt, OptiFine, Legacy Fabric, and LiteLoader.
- Fetch compatible loader versions, retain previews, and default to the latest stable release. Distinguish unavailable versions from request failures.
- Include Minecraft snapshots and historical Alpha / Beta versions, with type filters and the release-only default.
- Clear old selections and cancel requests when switching games or loaders. Reject late responses and disable installation while loading or without a compatible version.
- Use Meta profiles for Fabric, Quilt, and Legacy Fabric, and the bundled, unmodified HMCL engine for Forge, NeoForge, OptiFine, and LiteLoader processing and libraries.
- Fix historical native-library classifiers and obsolete ASM addresses. Resolve LiteLoader snapshots from official Maven metadata and obtain upstream checksums.
- Process loaders in a separate directory, verify generated files with SHA-256, and commit through the existing transaction. Roll back cancellation or failure and retain logs. Reuse a suitable local JDK or include Temurin JDK 21 or a higher game requirement in the plan.
- Check stable GitHub Releases from this repository, verify the single EXE's size and SHA-256, and replace and restart when idle. Defer while tasks, pending plans, or child windows are active.
- Use a separate update helper, a backup, and a new-window readiness check. Restore the previous program on failure, preserve externally modified files, and offer manual checking and an automatic-update switch.
- Theme the update menu so status text, actions, and check marks remain readable in light and dark modes. Defer executable replacement while the menu is open.
- Distinguish game-list loading, fetch failures, and empty filters. Reload on source changes, cancel obsolete requests, preserve existing lists on refresh failure, and offer to clear empty-result filters.
- Add a title-bar interface refresh action to rescan local versions and refresh the current section. Preserve resource filters and pagination, game and loader choices, and custom names; block duplicate refreshes while busy.
- After the first view appears, preload resource tags and each Modrinth section's first page before fetching up to six icons per section, so slow icons do not delay other lists. Reuse fresh caches and pending requests without changing the current view or downloading game payloads.
- Add version-list, preview, asynchronous-selection, installation, update-verification, and recovery checks.

## [0.1.3] - 2026-10-21

### 中文

- 增加侧栏启动入口，可直接打开已关联的 PCL / HMCL，使用启动器所在目录作为工作目录。
- 支持 HMCL JAR，通过 Java 17 或更高版本启动；可识别 MineDock 安装的 Java、已选 Java、JAVA_HOME、JRE_HOME 与 PATH，缺少运行环境时可手动选择。
- 内置原样的 PCL 2.99.0.0 和 HMCL 3.16.4 官方 EXE，支持在工具内选择目录、离线安装并自动关联对应 `.minecraft`。
- 安装时核对大小、SHA-256 与启动器身份；相同文件直接复用，不覆盖已有不同版本，失败或取消会清理临时文件。
- 安装任务执行期间禁止打开或安装启动器；启动过程中防止重复点击。
- 提供自带 .NET 运行时的 Windows x64 单 EXE，包含启动器、图标、原生组件、许可证与可导出的 HMCL / HMCLauncher 对应源码。
- 增加许可与源码窗口，保留第三方原始声明及 HMCL 附加条款；更正此前将 PCL 许可描述为 GPL 的错误。
- 增加离线安装、启动参数、文件保护、实际弹窗流程、明暗主题与最小窗口布局检查。
- 将侧栏选项和结果行的上浮改为由鼠标悬停控制：轻微上移并出现柔和阴影，移开后平滑落下，点击选中后保留强调色；按下提供回落与颜色反馈，点击范围保持稳定。
- 为模组、整合包、光影和材质包分别保存列表缓存，返回栏目时恢复筛选、页码、选中项目、文件版本和滚动位置；复用已获取的图标、标签和最近查看的文件版本。
- 列表写入本地缓存，24 小时内重新打开程序可恢复；刷新与新搜索重新请求，目标游戏变化时不使用旧目标列表。缓存异常不影响浏览，并防止快速切换时旧响应覆盖当前栏目。

### English

- Open the associated PCL / HMCL directly from the sidebar, using the launcher's folder as its working directory.
- Launch HMCL JAR files with Java 17 or later. Discover a selected runtime, MineDock-installed Java, JAVA_HOME, JRE_HOME, or PATH; allow manual runtime selection when needed.
- Bundle unmodified official PCL 2.99.0.0 and HMCL 3.16.4 executables for offline installation, directory selection, and automatic `.minecraft` association.
- Verify payload sizes, SHA-256 hashes, and launcher identities. Reuse identical files, preserve different existing launchers, and clean temporary files after failure or cancellation.
- Disable launcher actions during content installation and prevent repeated clicks during startup.
- Provide a self-contained Windows x64 single EXE containing the launchers, icons, native components, licenses, and exportable HMCL / HMCLauncher corresponding source.
- Add a license and source window, preserve upstream notices and additional terms, and correct the earlier inaccurate description of PCL as GPL-licensed.
- Add coverage for offline installation, launch arguments, file protection, the real modal workflow, themes, and minimum-window layout.
- Raise navigation options and result rows on hover with soft shadows, then return them smoothly to rest on pointer leave. Keep selected colors after clicking, add pressed feedback, and preserve stable hit targets.
- Cache each Modrinth section separately and restore filters, pagination, selection, file-version choice, and scroll position. Reuse loaded icons, tags, and recently viewed version lists.
- Persist lists locally for 24 hours, including across restarts. Refresh and new searches request new data; changed game targets do not reuse old-target lists. Cache failures remain nonfatal, and late responses cannot replace the current section.

## [0.1.2] - 2026-10-21

### 中文

- 将上一页与下一页统一为相同尺寸、样式和图标的镜像按钮。
- 显示当前页、总页数和项目总数，支持输入页码并回车或点击图标跳转，Esc 恢复当前页码。
- 放大页码字号与分页控件，修正文字内边距重复导致的遮挡，验证八位页码完整显示。
- 关联 PCL / HMCL 后自动选择配置中的游戏目录或同目录下的 `.minecraft`，保留手动选择；更换目标后清除原待安装计划。
- 增加 HMCL EXE / JAR 识别，以及旧版隔离配置、新版目录列表、设置预设、自定义运行目录和继承设置兼容；不改写启动器配置。
- 增加首页、末页、加载中和空结果的禁用状态，以及无效页码的就地提示。
- 翻页请求成功后才提交页码；失败时保留已有结果。新搜索重置到首页，结果减少时重新获取有效末页，过期请求不会覆盖新结果。
- 为选中的侧栏与结果增加轻微上浮、柔和阴影和强调标记；按钮悬停上浮、按下回落并保持点击区域稳定。补齐键盘焦点，结果淡入遵循系统动画开关。
- 新增可重复运行的 WPF 界面检查项目，并提供明暗主题、最小窗口的实际截图和验证记录。
- 约束虚拟化列表项的宽度，避免缩窄窗口后结果行右侧被裁切。

### English

- Matched the previous and next buttons with equal dimensions, styling, and mirrored icons.
- Display current and total pages and the total project count. Jump by entering a page and pressing Enter or clicking the icon; Escape restores the committed page.
- Enlarge page numbers and controls, fix clipping caused by duplicated text padding, and verify that eight-digit page numbers fit.
- Automatically select the configured game directory or a neighboring `.minecraft` after associating PCL / HMCL, with manual selection available. Clear the pending plan when the destination changes.
- Recognize HMCL EXE / JAR files and read legacy isolation settings, current directory lists, presets, custom run directories, and inheritance without rewriting launcher configuration.
- Disable unavailable actions at boundaries, during loading, and for empty results; show inline validation for invalid page numbers.
- Commit page changes only after successful requests. Preserve results on failure, reset new searches to page one, refetch the valid last page when results shrink, and reject stale responses.
- Raise selected navigation and results with soft shadows and accent markers. Buttons lift on hover and settle when pressed while retaining stable hit targets. Refine keyboard focus and result fades that respect the system animation setting.
- Add repeatable WPF UI checks, actual light / dark and minimum-window screenshots, and validation records.
- Constrain virtualized result widths so shrinking the window does not clip the right side of a row.

## [0.1.1] - 2026-10-21

### 中文

- 新增标题栏背景设置入口和图片预览。
- 支持 PNG、JPEG、WebP、BMP，自定义强度和填充 / 适应布局。
- 自动缓存所选图片并记忆设置；移动原图后仍可显示，支持恢复默认。
- 明暗主题分别适配显示强度和次要文字对比度。
- 损坏图片保留当前背景，缺失缓存时安全回退。
- 20 项背景专项检查和 5 项便携版启动检查通过。

### English

- Added title bar background settings and an image preview.
- Support PNG, JPEG, WebP, and BMP with intensity and fill / fit controls.
- Cache selected images and retain settings across launches; moving the original file does not remove the background.
- Adapt background strength and secondary text contrast to light and dark themes.
- Preserve the current background on invalid input and fall back safely when cached files are missing.
- Passed 20 focused background checks and 5 portable startup checks.

## [0.1.0] - 2026-10-21

### 中文

- 首个可运行的 Windows x64 便携版本。
- 原版正式版、Fabric 和 Fabric API 安装。
- Modrinth 搜索、栏目筛选、模组依赖解析及资源下载 / 安装。
- Temurin Java 安装，PCL 隔离设置读取，安装计划、校验、缓存、断点续传与回滚。
- 明暗主题和中文 WPF 界面。
- Forge 等其他加载器、整合包自动安装与 ZIP 解包尚未实现。

### English

- First functional Windows x64 portable release.
- Official Minecraft release, Fabric, and Fabric API installation.
- Modrinth search, section filters, mod dependency resolution, and content downloads / installation.
- Temurin Java installation, PCL isolation settings integration, installation plans, verification, caching, resuming, and rollback.
- Light and dark themes with a Chinese-language WPF interface.
- Other loaders such as Forge, automatic modpack installation, and ZIP extraction are not implemented yet.

[0.1.0]: https://github.com/EnchantGapple233/Minedock/releases/tag/v0.1.0
[0.1.1]: https://github.com/EnchantGapple233/Minedock/releases/tag/v0.1.1
[0.1.2]: https://github.com/EnchantGapple233/Minedock/releases/tag/v0.1.2
[0.1.3]: https://github.com/EnchantGapple233/Minedock/releases/tag/v0.1.3
[0.1.4]: https://github.com/EnchantGapple233/Minedock/releases/tag/v0.1.4
