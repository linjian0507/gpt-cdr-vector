# CorelDRAW SVG Workflow

Use direct SVG generation when the artwork should stay editable in CorelDRAW. This is best for logos, icons, decals, stickers, packaging marks, labels, flat illustrations, line art, engraving, and cutter/plotter paths.

Use raster generation plus tracing only when the user wants painterly, photorealistic, textured, or complex shaded artwork. In that case, generate a raster reference image, trace it with CorelDRAW PowerTRACE, then manually simplify and recolor the result.

## 中文说明

当图形需要在 CorelDRAW 中继续编辑时，应优先直接生成 SVG。这个方式最适合 Logo、图标、贴花、贴纸、包装标识、标签、扁平插画、线稿、雕刻稿和切割/绘图机路径。

只有在用户明确想要手绘感、写实照片感、复杂纹理或复杂明暗效果时，才建议先生成位图参考，再用 CorelDRAW PowerTRACE 追踪成矢量。追踪后通常还需要人工简化节点、重新配色和整理路径，否则文件可能过重且不利于生产。

## CorelDRAW-Friendly SVG Constraints

- Prefer basic shapes and paths.
- Keep gradients simple and optional.
- Avoid SVG filters, blur, masks, clipping, animations, external fonts, CSS imports, embedded images, and links.
- Use a `viewBox`, `width`, and `height`.
- Use named groups for major artwork regions.
- Keep fill and stroke colors explicit.
- For cut lines, use closed paths and a single named spot color convention when the production shop requires it.

### 中文约束

- 优先使用基础形状和路径，减少复杂 SVG 特性。
- 渐变应保持简单，并且只在确实需要时使用。
- 避免滤镜、模糊、遮罩、裁剪、动画、外部字体、CSS 导入、内嵌图片和外部链接。
- SVG 必须包含 `viewBox`、`width` 和 `height`，方便 CorelDRAW 正确识别画布。
- 主要图形区域建议使用命名分组，便于导入后选择和整理。
- 填充色和描边色应明确写出，减少导入后颜色丢失或继承异常。
- 切割线应使用闭合路径；如果加工方要求专色或指定线名，需要按其命名规范处理。

## Cleanup After Import

Common CorelDRAW cleanup steps:

1. Ungroup imported artwork.
2. Convert outlines/strokes to objects when the vendor needs filled shapes.
3. Convert text to curves before final delivery unless editable text is required.
4. Weld, trim, or combine overlapping shapes.
5. Simplify nodes on traced or highly detailed paths.
6. Save the working file as `.cdr`; export SVG/PDF/EPS only for delivery.

### 中文导入后整理

常见整理步骤如下：

1. 取消导入图形的群组，方便选择单个对象。
2. 加工方需要填充形状时，把轮廓线/描边转换为对象。
3. 除非明确要求文字可编辑，最终交付前应把文字转为曲线。
4. 对重叠形状进行焊接、修剪或合并，减少生产问题。
5. 对追踪得到的路径或节点过多的路径进行节点简化。
6. 工作文件保存为 `.cdr`；需要交付给外部时再导出 SVG、PDF 或 EPS。

## Prompt Pattern

Ask for production constraints explicitly:

```text
Create a CorelDRAW-ready SVG logo mark for [brand/use].
Style: [flat/geometric/line art/badge/sticker].
Canvas: 1024x1024 viewBox 0 0 1024 1024.
Colors: [2-4 colors].
No raster images, no filters, no external fonts.
Use closed paths and named groups.
Output only valid SVG XML.
```

### 中文提示词模板

生成提示词时应明确生产约束，例如：

```text
为 [品牌/用途] 创建一个适合 CorelDRAW 的 SVG Logo 标识。
风格：[扁平/几何/线稿/徽章/贴纸]。
画布：1024x1024，viewBox 0 0 1024 1024。
颜色：[2-4 个颜色]。
不要位图、不要滤镜、不要外部字体。
使用闭合路径和命名分组。
只输出有效的 SVG XML。
```
