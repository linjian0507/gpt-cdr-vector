---
name: gpt-cdr-vector
description: Generate CorelDRAW-ready vector artwork from GPT prompts. Use when Codex needs to create, refine, validate, or import SVG/EPS-like vector graphics for CorelDRAW/CDR workflows, including layered posters, logos, icons, flat illustrations, stickers, line art, engraving artwork, cutting/plotter shapes, and editable SVG assets intended to become .CDR files.
---

# GPT CDR Vector

Create editable vector artwork for CorelDRAW by having GPT output clean SVG, then validate and import the SVG into CDR. Prefer direct SVG generation for layered posters, logos, icons, labels, stickers, flat illustrations, line art, and plotter/cutter artwork.

## 中文说明

本技能用于把文字需求转成适合 CorelDRAW 使用的可编辑矢量 SVG。适用场景包括复杂分层海报、Logo、图标、贴纸、标签、线稿、雕刻稿、切割/绘图机路径和扁平插画等需要后续进入 `.cdr` 工作流的图形。

推荐优先生成干净的 SVG，而不是生成位图再追踪。这样导入 CorelDRAW 后更容易保留图层、路径、基础形状、颜色和轮廓，便于继续编辑、转曲、焊接、裁切、简化节点或另存为 `.cdr`。

## Workflow

1. Clarify the SVG preset/task type first: icon, logo, single object, reference-to-SVG, subject extraction, line art, cut path, sticker, label, poster, infographic, diagram, pattern, or background.
2. Generate SVG with `scripts/generate_cdr_svg.py` unless the user already supplied SVG:

```bash
python scripts/generate_cdr_svg.py "minimal coffee shop badge logo, two colors, no text" --preset logo --style logo --colors "#111111,#f4c542" --output coffee-badge.svg
```

3. Inspect the SVG before handing it off. It should contain one `<svg>` document, a `viewBox`, no Markdown fences, no external image links, no scripts, and no embedded raster images.
4. Use the preset to guide grouping: `icon_mark`, `logo_mark`, `subject`, `diagram_nodes`, `connectors`, `label_layout`, `background`, `typography`, and similar editable layer names.
5. When a reference image is supplied, use `--preset image-to-svg` or `--preset subject` when appropriate, and rebuild the artwork as native SVG shapes.
6. Import the SVG into CorelDRAW. Use the Addons `app.exe` package when CorelDRAW has no VBA, and use `assets/coreldraw_gpt_vector.bas` only when the user wants a macro bridge.
7. After import, recommend CDR-side cleanup when needed: ungroup, convert strokes to objects, convert text to curves, weld/trim, simplify nodes, and save as `.cdr`.

### 中文流程

1. 先确认 SVG 预设/任务类型：图标、Logo、单物体、参照图转 SVG、提取主体、线稿、切割路径、贴纸、标签、海报、信息图、流程图、图案或背景。
2. 如果用户没有提供 SVG，就用 `scripts/generate_cdr_svg.py --preset ...` 根据提示词生成 SVG。
3. 交付前检查 SVG：必须是单个 `<svg>` 文档，包含 `viewBox`，不能有 Markdown 代码围栏、外部图片链接、脚本或内嵌位图。
4. 根据预设要求模型使用可编辑图层名，例如 `icon_mark`、`logo_mark`、`subject`、`diagram_nodes`、`connectors`、`label_layout`、`background`、`typography` 等。
5. 如果用户提供参照图，按场景使用 `image-to-svg` 或 `subject` 预设，只把参照图作为版式、层级、配色和风格参考，仍然要求模型重建为原生 SVG 图形。
6. 需要进入 CorelDRAW 时，可手动导入 SVG；当前 CorelDRAW 没有 VBA 时优先使用 Addons `app.exe` 包，只有明确需要宏桥接时再使用 `assets/coreldraw_gpt_vector.bas`。
7. 导入后根据生产要求做 CDR 侧整理，例如取消群组、描边转对象、文字转曲、焊接/修剪、简化节点并保存为 `.cdr`。

## Generation Rules

- Ask GPT for native SVG, not a raster image, when the final asset must be editable in CorelDRAW.
- Keep CorelDRAW compatibility high: prefer `<path>`, `<rect>`, `<circle>`, `<ellipse>`, `<polygon>`, `<polyline>`, simple gradients, flat fills, and strokes.
- Avoid or replace SVG features that import poorly: filters, blur, masks, `foreignObject`, CSS animations, external fonts, external links, and embedded `<image>` raster data.
- For production logos, request no live text unless the user explicitly wants editable text. Imported font differences are common; text-to-curves is safer for final artwork.
- For cutter/plotter work, request closed paths, no overlapping duplicate strokes, and a single spot color or named cut line.
- For photorealistic artwork, explain that GPT image models usually produce raster art. Use a raster image as a reference, then vectorize with CorelDRAW PowerTRACE or rebuild as simplified SVG.

### 中文生成规则

- 最终需要可编辑矢量时，应要求模型输出原生 SVG，不要输出位图。
- 为提高 CorelDRAW 兼容性，优先使用路径、矩形、圆形、椭圆、多边形、折线、简单渐变、纯色填充和普通描边。
- 尽量避免滤镜、模糊、遮罩、`foreignObject`、CSS 动画、外部字体、外部链接和内嵌 `<image>` 位图，这些内容导入 CorelDRAW 后容易丢失或变形。
- 正式 Logo 成品通常不建议保留实时文字，除非用户明确要求文字可编辑；交付前转曲更稳妥。
- 切割/绘图机用途应强调闭合路径、无重复重叠线条，并按加工方要求使用单一专色或命名切割线。
- 如果用户要写实、厚涂、复杂纹理或照片质感，应说明这类需求更适合先生成位图参考，再用 CorelDRAW PowerTRACE 或人工重建为简化矢量。

## CorelDRAW Macro Bridge

Use `assets/coreldraw_gpt_vector.bas` when the user wants CorelDRAW to generate and import the SVG in one action. The macro expects:

- `OPENAI_RELAY_API_KEY` or `OPENAI_API_KEY` set in the Windows environment.
- Python available as `python`.
- `GPT_CDR_VECTOR_SKILL_DIR` optionally set to this skill directory. If unset, the macro asks for the skill path.

The macro prompts for artwork text, calls `scripts/generate_cdr_svg.py`, writes an SVG to `%TEMP%`, and imports it into the active CorelDRAW document.

For a plugin-style CorelDRAW experience without VBA, build the Addons app package with `scripts/build_coreldraw_addon.ps1 -Zip` and run `dist/coreldraw_addon/GptCdrVectorInstaller.exe`. The installer is self-contained, embeds the `gpt-cdr-vector` Addons package, auto-detects registered CorelDRAW `Programs64\Addons` roots, installs the embedded package, and writes `corel_version.txt` per target version. Both the installer and `app.exe` include an API settings page for `OPENAI_RELAY_API_KEY`, `OPENAI_VECTOR_API_URL`, `OPENAI_VECTOR_MODEL`, and `OPENAI_API_TIMEOUT`. The package contains `CorelDrw.addon`, `AppUI.xslt`, `GptCdrVectorHost.dll`, `app.exe`, `config.json`, `msc.json`, and `uisettings.ini`; CorelDRAW should load the WPF host as a fixed top toolbar button that opens `app.exe`.

For a plugin-style CorelDRAW experience on systems with VBA installed, use the GMS/VBA panel source in `assets/coreldraw_plugin/` and follow `docs/coreldraw-plugin-install.md`. When CorelDRAW cannot initialize VBA, use the Addons app package first or the non-VBA external panel in `scripts/gpt_cdr_vector_panel.ps1` as a fallback.

The non-VBA panel defaults to the third-party text relay `https://ai.opendoor.sbs/v1/chat/completions` with model `gpt-4.1-mini`; set `OPENAI_RELAY_API_KEY`. For complex layered posters, use `OPENAI_VECTOR_MODEL=gpt-4.1` when the relay account supports it.

The non-VBA panel can pass a PNG/JPG/WEBP reference image to the relay as a multimodal Chat Completions message. The relay model must support image input.

For slow relay models or reference-image generation, increase `OPENAI_API_TIMEOUT`; the panel defaults to 600 seconds.

The Addon panel includes SVG presets for icon/button, logo/wordmark, single object, reference-to-SVG, subject extraction, line art, cutting/engraving, sticker/badge, product label, poster, infographic, diagram, pattern, and background. Preset controls the SVG production task; visual style only affects appearance. Non-poster presets must suppress poster/card/page layout, and reference images for subject/object/icon presets should guide only the isolated asset, not the full source layout. References can come from an image file, the current CorelDRAW selection exported to a temporary PNG, or a pasted clipboard screenshot.

The non-VBA panel includes a model selector for `gpt-5.4-mini`, `gpt-5.4`, `gpt-5.5`, and `gpt-4.1-mini`; it defaults to `gpt-5.4-mini` to reduce draft cost.

### 中文宏说明

当用户希望在 CorelDRAW 内直接输入需求、生成 SVG 并导入当前文档时，可使用 `assets/coreldraw_gpt_vector.bas`。使用前需要确保 Windows 环境变量中有 `OPENAI_RELAY_API_KEY` 或 `OPENAI_API_KEY`，并且命令行可直接运行 `python`。

如果设置了 `GPT_CDR_VECTOR_SKILL_DIR`，宏会直接使用该技能目录；如果没有设置，宏会弹窗要求用户输入技能目录路径。宏会把生成结果写入 `%TEMP%\gpt-cdr-vector.svg`，然后导入到当前 CorelDRAW 活动图层。

如果需要更接近插件工具箱的体验且 CorelDRAW 没有 VBA，优先运行 `scripts/build_coreldraw_addon.ps1 -Zip` 生成 Addons 外部程序包，再运行 `dist/coreldraw_addon/GptCdrVectorInstaller.exe`。安装器是单文件完整安装包，内嵌 `gpt-cdr-vector` Addons 包，会自动识别已注册的 CorelDRAW `Programs64\Addons` 目录并安装，按目标版本写入 `corel_version.txt`。安装器和 `app.exe` 都提供 API 设置页，可配置 `OPENAI_RELAY_API_KEY`、`OPENAI_VECTOR_API_URL`、`OPENAI_VECTOR_MODEL` 和 `OPENAI_API_TIMEOUT`。该包包含 `CorelDrw.addon`、`AppUI.xslt`、`GptCdrVectorHost.dll`、`app.exe`、`config.json`、`msc.json` 和 `uisettings.ini`；CorelDRAW 会把 WPF 宿主加载成顶部固定工具栏按钮，点击后打开 `app.exe`。

如果 CorelDRAW 已安装 VBA，也可使用 `assets/coreldraw_plugin/` 中的 GMS/VBA 面板源码，并按照 `docs/coreldraw-plugin-install.md` 安装。若 CorelDRAW 提示无法初始化 VBA，则使用 Addons 外部程序包，或把 `scripts/gpt_cdr_vector_panel.ps1` 非 VBA 外部面板作为备用。

非 VBA 面板默认使用第三方文本中转 `https://ai.opendoor.sbs/v1/chat/completions` 和 `gpt-4.1-mini` 生成 SVG；需设置 `OPENAI_RELAY_API_KEY`。复杂分层海报建议在中转账号支持时设置 `OPENAI_VECTOR_MODEL=gpt-4.1`。

非 VBA 面板可把 PNG/JPG/WEBP 参照图作为多模态消息传给中转接口；中转模型必须支持图片输入。

慢模型或带参照图生成海报时可调大 `OPENAI_API_TIMEOUT`；面板默认等待 600 秒。

Addon 面板提供 SVG 预设，可选图标/按钮、Logo/字标、单物体主体、参照图转 SVG、提取主体、线稿轮廓、切割雕刻、贴纸徽章、产品标签、可编辑海报、信息图表、流程图解、无缝图案和背景纹理。预设决定 SVG 生产任务，风格只影响视觉表现。

非 VBA 面板提供模型下拉选择，可在 `gpt-5.4-mini`、`gpt-5.4`、`gpt-5.5`、`gpt-4.1-mini` 间切换；默认选 `gpt-5.4-mini` 以降低试稿成本。

## Useful References

- Read `references/coreldraw-svg-workflow.md` when the task involves CDR import cleanup, plotter/cutter output, or deciding between direct SVG and raster tracing.

## 中文参考

- 当任务涉及 CorelDRAW 导入清理、切割/绘图机输出，或需要判断“直接生成 SVG”还是“位图生成后追踪”时，阅读 `references/coreldraw-svg-workflow.md`。
