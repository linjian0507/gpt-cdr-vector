# CorelDRAW 插件式安装说明

本项目提供三种 CorelDRAW 使用方式：

1. **Addons 外部程序包**：推荐给当前 CorelDRAW 2018 环境使用。它是类似截图目录结构的 `app.exe` 包，不依赖 VBA，也不依赖 Python 生成。
2. **非 VBA PowerShell 面板**：备用方案。它通过 Windows COM 连接 CorelDRAW，不需要 CorelDRAW 宏，但仍依赖 Python 脚本。
3. **GMS/VBA 插件面板**：仅适合已安装 VBA 的 CorelDRAW。没有安装 VBA 时不能使用。

你当前截图中的错误是 CorelDRAW 2018 无法初始化 VBA，所以应优先使用“Addons 外部程序包”，PowerShell 面板作为备用。

## Addons 外部程序包

生成安装包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build_coreldraw_addon.ps1 -Zip
```

生成后的目录：

```text
dist\coreldraw_addon\gpt-cdr-vector
```

推荐运行单文件自动安装器：

```text
dist\coreldraw_addon\GptCdrVectorInstaller.exe
```

安装器已内嵌完整 `gpt-cdr-vector` 插件包，可以单独分发，不需要用户同时下载 zip。安装器会自动识别本机已注册的 CorelDRAW 版本和 `Programs64\Addons` 目录，默认勾选检测到的版本。点击“安装插件”后，会把内嵌插件包复制到对应版本的 Addons 目录，并写入该版本对应的 `corel_version.txt`，让面板连接当前版本的 CorelDRAW。

安装器右侧有“设置 API”按钮，可直接配置：

```text
OPENAI_RELAY_API_KEY
OPENAI_VECTOR_API_URL
OPENAI_VECTOR_MODEL
OPENAI_API_TIMEOUT
```

这些配置会写入当前 Windows 用户环境变量。安装后打开插件面板，也可以点击面板里的“设置”按钮再次更换 Key、接口、默认模型或超时时间。

主要文件：

```text
GptCdrVectorInstaller.exe
gpt-cdr-vector\
app.exe
GptCdrVectorHost.dll
CorelDrw.addon
AppUI.xslt
UserUI.xslt
config.json
msc.json
uisettings.ini
README_INSTALL.txt
```

安装方式有三种：

1. 推荐：运行单个 `GptCdrVectorInstaller.exe`，勾选识别到的 CDR 版本，点击“安装插件”。
2. 备用：从 zip 或构建目录中手动复制整个 `gpt-cdr-vector` 文件夹到 CorelDRAW 的 `Programs64\Addons` 根目录下。
3. 或运行安装脚本，把包复制到你指定的 Addons 根目录：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install_coreldraw_addon.ps1 -AddonsRoot "E:\软件\cdr集合\2018x一键安装\CDR2018\Programs64\Addons"
```

安装脚本会写入：

```text
E:\软件\cdr集合\2018x一键安装\CDR2018\Programs64\Addons\gpt-cdr-vector
```

不会修改截图中的 `qiuku\orc` 目录。若目标目录已存在，脚本会停止；确认覆盖文件时再加 `-Force`。

使用前设置中转 Key：

```powershell
setx OPENAI_RELAY_API_KEY "你的中转 API Key"
```

可选设置：

```powershell
setx OPENAI_API_TIMEOUT "900"
setx GPT_CDR_VECTOR_COREL_VERSION "20"
```

`GPT_CDR_VECTOR_COREL_VERSION=20` 对应 CorelDRAW 2018。默认就是 `20`。

安装后，`CorelDrw.addon` 会让 CorelDRAW 扫描该目录，`AppUI.xslt` 会把 `GptCdrVectorHost.dll` 固定到顶部工具栏。工具栏按钮点击后打开 `app.exe` 面板；`app.exe` 会直接调用中转接口生成可编辑 SVG，再通过 CorelDRAW COM 导入当前文档。它保留 API 设置、模型选择、参照图、运行日志、只生成 SVG、生成并导入、导入已有文件等功能。参照图可以从文件选择，也可以把 CorelDRAW 当前选中对象导出为临时 PNG，或直接粘贴剪贴板截图。

面板新增 `SVG预设`，用于选择 SVG 生产任务，而不是生图模型场景。可选项包括 `不使用预设`、`图标/按钮`、`Logo/字标`、`单物体主体`、`参照图转SVG`、`提取主体`、`线稿轮廓`、`切割雕刻`、`贴纸徽章`、`产品标签`、`可编辑海报`、`信息图表`、`流程图解`、`无缝图案`、`背景纹理`。预设决定生成目标，`风格` 只决定视觉表现；二者冲突时优先按 `SVG预设` 执行。非海报预设会自动压制 `poster` 风格，避免继续生成卡片、海报或整页版式。

参照图旁边的 `先识图生成 1:1 提示词` 开关用于两段式生成：当已经选择图片、使用 CDR 选中对象或粘贴截图后，面板会先调用一次视觉模型，把图片转换成尽量 1:1 还原的 SVG 生成提示词，并自动写回 `图形描述`；随后再用这段提示词继续生成 SVG。该模式更适合还原现有截图、包装稿、图标或版式，但会多消耗一次模型请求时间和费用。

生成提示词已按 SVG 生产流程优化：会要求模型先内部规划任务类型、几何结构、版式、视觉层级、色彩、留白、图形密度和 CorelDRAW 图层结构，再输出 SVG；同时会避免稀疏图标排布、随机品牌名、伪文字、大面积空白和占位式模板。该优化能提高构图完整度和可编辑层次，但纯文本模型生成 SVG 仍不等同于专业位图海报模型，复杂写实效果建议先用参照图或更强模型试稿。

注意：不同 CorelDRAW 整合包的工作区缓存行为不完全相同。如果复制后没有自动出现顶部工具栏，先确认目录名必须是 `Programs64\Addons\gpt-cdr-vector`，然后重启 CorelDRAW；仍未出现时，按住 `F8` 启动 CorelDRAW 让工作区重新应用 Addons 的 UI 变换。

## 非 VBA PowerShell 面板

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\gpt_cdr_vector_panel.ps1 -CorelVersion 20
```

也可以双击：

```text
scripts\start_gpt_cdr_vector_panel.cmd
```

面板会连接 CorelDRAW 2018，调用 Python 通过中转文本接口生成可编辑 SVG，然后通过 CorelDRAW COM 接口导入当前文档。这个方案不依赖 VBA，也不会触发“无法初始化 Visual Basic for Applications”的错误。

面板支持选择 PNG/JPG/WEBP 参照图。参照图会作为版式、层级、配色和风格参考传给中转文本模型，生成结果仍然是可编辑 SVG；脚本不会把参照图作为位图嵌入 SVG。该功能要求中转模型支持图片输入，如果接口返回“不支持图片/vision/image_url”之类错误，需要换支持视觉输入的模型或先不用参照图。

当前可编辑 SVG 矢量接口为：

```text
POST https://ai.opendoor.sbs/v1/chat/completions
model: gpt-4.1-mini
Authorization: Bearer <中转 API Key>
```

先设置中转 Key：

```powershell
setx OPENAI_RELAY_API_KEY "你的中转 API Key"
```

面板内可以直接选择模型，并默认选择 `gpt-5.4-mini` 以降低试稿成本。成本优先建议先用 `gpt-5.4-mini`，质量和成本折中用 `gpt-5.4`，只有确实需要更强输出时再用 `gpt-5.5`。

也可以用环境变量指定默认模型：

```powershell
setx OPENAI_VECTOR_MODEL "gpt-5.4-mini"
```

使用 `gpt-5.5`、`gpt-5.5-pro` 或带参照图生成复杂海报时，接口可能需要几分钟。面板默认等待 `600` 秒，如仍超时可调大：

```powershell
setx OPENAI_API_TIMEOUT "900"
```

设置后重新打开面板。

先测试连接：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\gpt_cdr_vector_panel.ps1 -CorelVersion 20 -SelfTest
```

如果生成时报鉴权、余额或模型不可用，说明 CorelDRAW 和 Python 通路大概率已经通了，但中转平台的 Key、余额、模型名或接口权限需要检查。可先确认 `OPENAI_RELAY_API_KEY` 是否为中转平台生成的 Key，而不是 OpenAI 官方 Key。

如果生成时报 `read operation timed out`，说明接口在本地等待时间内没有返回。优先把 `OPENAI_API_TIMEOUT` 调到 `900`，如果仍然超时，再降低模型、缩小尺寸或去掉参照图重试；也可能是中转平台自身提前断开长请求。

如果生成时报 `基础连接已经关闭` 或 `连接被意外关闭`，通常不是 CorelDRAW 导入问题，而是中转平台/网络网关在长时间无返回数据时中断了非流式请求。新版 Addon 对 `chat/completions` 和 `responses` 都启用 `stream=true` 流式返回，用于避免 60 秒空闲断开；如果仍然出现该错误，优先换 `gpt-5.4-mini`、缩小尺寸、减少参照图复杂度，或确认当前中转接口支持 OpenAI 兼容流式返回。

如果面板的 `接口` 下拉框是 `环境变量`，会强制使用 `OPENAI_VECTOR_API_URL`。一般建议保持 `自动`，让面板按模型选择合适接口并启用流式返回；只有明确知道中转接口要求时再手动覆盖。

## GMS/VBA 插件面板

下面这一节仅适用于已安装 VBA 的 CorelDRAW。

本项目也保留 CorelDRAW GMS/VBA 宏项目形式来实现“插件式”使用体验：安装后在 CorelDRAW 的脚本管理器中出现入口，可把入口添加到菜单或工具栏，运行后打开一个常驻的 GPT CDR Vector 面板。

这不是直接改写 `.cdr` 文件，也不是 DLL 级 CorelDRAW Addon。它保留现有 Python 生成链路：

```text
CorelDRAW 面板 -> Python 脚本 -> 中转 chat/completions 接口 -> SVG -> CorelDRAW 导入
```

### 安装准备

1. 确认 CorelDRAW 已关闭。
2. 确认 Windows 命令行可运行 `python`。
3. 设置 `OPENAI_RELAY_API_KEY` 用户环境变量。
4. 运行安装准备脚本：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install_coreldraw_plugin.ps1 -ListTargets
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install_coreldraw_plugin.ps1 -CorelVersion 24
```

`-CorelVersion 24` 对应 CorelDRAW 2022。脚本会定位用户 GMS 目录，复制插件源码，并设置 `GPT_CDR_VECTOR_SKILL_DIR`。

### 导入到 CorelDRAW

1. 打开 CorelDRAW。
2. 打开脚本管理器：`工具 > 脚本 > 脚本`，或按 `Alt+Shift+F11`。
3. 新建一个 VBA 宏项目，建议命名为 `GptCdrVectorPlugin.gms`，并保存在用户 GMS 目录。
4. 打开脚本编辑器/Visual Basic Editor。
5. 导入安装脚本复制出的两个文件：
   - `GptCdrVectorPlugin.bas`
   - `GptCdrVectorPanel.frm`
6. 运行入口：

```text
GptCdrVectorPlugin.ShowGptCdrVectorPanel
```

### 做成工具栏按钮

导入成功后，可在 CorelDRAW 的自定义设置里把 `GptCdrVectorPlugin.ShowGptCdrVectorPanel` 添加到工具栏或菜单。这样使用体验就接近截图中的工具箱按钮：点击按钮打开 GPT CDR Vector 面板。

## 面板功能

- `只生成 SVG`：通过中转文本接口生成 SVG 文件，不导入当前文档。
- `生成并导入`：生成 SVG 后导入当前活动图层。
- `导入已有 SVG`：选择已生成的 SVG 路径并导入。
- `模型`：在 `gpt-5.4-mini`、`gpt-5.4`、`gpt-5.5`、`gpt-4.1-mini` 之间切换；日志会显示本次实际使用的模型。
- `SVG预设`：选择 SVG 使用场景，如图标、Logo、单物体、参照图转 SVG、提取主体、线稿、切割雕刻、贴纸、标签、海报、信息图、流程图、图案和背景。
- `参照图`：选择一张 PNG/JPG/WEBP 图片作为预设参考，也可以点 `用选中` 把 CorelDRAW 页面当前选中对象导出成临时 PNG，或点 `粘贴` 读取剪贴板截图；`提取主体`、`单物体主体`、`图标/按钮` 等预设只参考主体，不保留海报或页面版式；`参照图转SVG` 才会尽量保留整体布局。
- `先识图生成 1:1 提示词`：有参照图时先调用模型把图片整理成可还原的 SVG 生成提示词，自动回填到 `图形描述`，再继续生成 SVG；这会多一次接口请求。
- `运行日志`：按时间显示当前任务步骤，例如读取配置、调用模型、等待模型返回、生成文件、导入 CorelDRAW、完成或失败。
- `风格`：对应脚本中的 `auto`、`poster`、`logo`、`icon`、`sticker`、`illustration` 等风格预设；默认使用 `auto`，复杂精美海报才建议切到 `poster`。
- `尺寸`：对应生成脚本的 `--size` 参数。
- `颜色`：可填写如 `#111111,#f4c542` 的颜色列表。
- `允许 SVG 文字`：需要可编辑文字时打开；正式 Logo 交付通常建议关闭并转曲。

## 回滚

1. 在 CorelDRAW 脚本管理器中卸载或删除 `GptCdrVectorPlugin.gms`。
2. 删除用户 GMS 目录下的 `gpt-cdr-vector-plugin-source` 文件夹。
3. 如需清理环境变量，可运行：

```powershell
[Environment]::SetEnvironmentVariable("GPT_CDR_VECTOR_SKILL_DIR", $null, "User")
```
