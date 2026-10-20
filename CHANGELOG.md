# Changelog / 更新日志

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
