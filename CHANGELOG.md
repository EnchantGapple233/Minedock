# Changelog / 更新日志

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
