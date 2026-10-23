# 社交预览图 / Social Preview

[返回首页](../README.md)

成品：[social-preview.png](images/social-preview.png)，1280 × 640 PNG，726,318 字节。用于 GitHub 仓库 Settings → General → Social preview；不替换 README 中未经修改的[实际界面截图](images/v0.1.4-main.png)。

The repository preview uses a 1280 × 640 PNG, 726,318 bytes. It is separate from the unmodified screenshot shown in the README.

2026-10-23 使用内置 `image_gen` 编辑模式，以 `docs/images/v0.1.4-main.png` 为输入生成；输出保持 2:1 比例，再等比例缩至 1280 × 640，没有裁切。工具生成的预览画面经过视觉检查；实际界面文字以原始截图为准。

Created on 2026-10-23 using the built-in `image_gen` edit mode with the real screenshot as input, then proportionally resized to 1280 × 640 without cropping. The generated preview was visually reviewed; the original screenshot remains authoritative for UI text.

## 生成提示词 / Prompt

```text
Create one polished GitHub social preview image for the open source Windows
application MineDock, using the attached REAL application screenshot as the
central visual. Target output EXACTLY 1280 pixels wide by 640 pixels high
(2:1 aspect ratio), under 1 MB if possible.

This is a repository share preview, not a redesigned application UI.
Preserve the attached screenshot's real screen contents and layout: do not
invent app controls, redraw game versions, change text or logos, or add
fictitious screens. The screenshot may be uniformly scaled smaller and
placed on the composition with a subtle shadow, but its visible contents
must remain faithful.

Put a large, perfectly legible text heading 'MineDock' near the top left
with a smaller single supporting line 'Minecraft Java downloader & installer'.
Put a small line along the bottom left
'Forge / NeoForge / Fabric / Quilt / OptiFine'.
All added text is exactly as quoted, no other text.

Composition: neutral near-white background, forest-green accent used
sparingly, charcoal black heading, and a real dark application screenshot
spanning most of the canvas centered below the title. Keep the full
screenshot visible, no crop; it should take up about 75 percent of image
height. A simple, clear horizontal composition with crisp typography and
generous but efficient spacing; no decorative gradients, orbs,
illustrations, badges, made-up logos, or stock art.

All critical elements at least 32 pixels from the canvas edges. Aim for an
honest, practical desktop software project preview with immediately
recognizable MineDock name and the actual application.
```
