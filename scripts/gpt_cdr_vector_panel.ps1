param(
    [string]$CorelVersion = "20",
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$generator = Join-Path $repoRoot "scripts\generate_cdr_svg.py"
$textApiUrl = "https://ai.opendoor.sbs/v1/chat/completions"
$textModel = "gpt-4.1-mini"
$script:ReferenceImagePath = ""
$script:LogBox = $null

function Get-VectorModel {
    $model = [Environment]::GetEnvironmentVariable("OPENAI_VECTOR_MODEL", "Process")
    if ([string]::IsNullOrWhiteSpace($model)) {
        $model = [Environment]::GetEnvironmentVariable("OPENAI_VECTOR_MODEL", "User")
    }
    if ([string]::IsNullOrWhiteSpace($model)) {
        return $textModel
    }
    return $model
}

function Get-VectorApiUrl {
    $apiUrl = [Environment]::GetEnvironmentVariable("OPENAI_VECTOR_API_URL", "Process")
    if ([string]::IsNullOrWhiteSpace($apiUrl)) {
        $apiUrl = [Environment]::GetEnvironmentVariable("OPENAI_VECTOR_API_URL", "User")
    }
    if ([string]::IsNullOrWhiteSpace($apiUrl)) {
        return $textApiUrl
    }
    return $apiUrl
}

function Get-ApiTimeout {
    $timeout = [Environment]::GetEnvironmentVariable("OPENAI_API_TIMEOUT", "Process")
    if ([string]::IsNullOrWhiteSpace($timeout)) {
        $timeout = [Environment]::GetEnvironmentVariable("OPENAI_API_TIMEOUT", "User")
    }
    if ([string]::IsNullOrWhiteSpace($timeout)) {
        return "600"
    }
    return $timeout
}

function Get-CorelDrawApp {
    param([string]$Version)

    $progId = "CorelDRAW.Application.$Version"
    try {
        return [Runtime.InteropServices.Marshal]::GetActiveObject($progId)
    } catch {
    }

    try {
        $app = New-Object -ComObject $progId
        try { $app.Visible = $true } catch {}
        return $app
    } catch {
        throw "无法连接 $progId。请确认 CorelDRAW 已安装并已启动。"
    }
}

function Sync-OpenAIEnvironment {
    foreach ($name in @("OPENAI_RELAY_API_KEY", "OPENAI_API_KEY", "OPENAI_VECTOR_MODEL", "OPENAI_VECTOR_API_URL", "OPENAI_RESPONSES_API_URL", "OPENAI_API_TIMEOUT")) {
        $value = [Environment]::GetEnvironmentVariable($name, "Process")
        if ([string]::IsNullOrWhiteSpace($value)) {
            $value = [Environment]::GetEnvironmentVariable($name, "User")
            if (![string]::IsNullOrWhiteSpace($value)) {
                [Environment]::SetEnvironmentVariable($name, $value, "Process")
            }
        }
    }
}

function Add-PanelLog {
    param([string]$Message)

    if ($null -eq $script:LogBox) {
        return
    }

    $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $Message
    $script:LogBox.AppendText($line + [Environment]::NewLine)
    $script:LogBox.SelectionStart = $script:LogBox.TextLength
    $script:LogBox.ScrollToCaret()
    $script:LogBox.Refresh()
}

function Clear-PanelLog {
    if ($null -ne $script:LogBox) {
        $script:LogBox.Clear()
    }
}

function Test-PanelEnvironment {
    if (!(Test-Path $generator)) {
        throw "找不到生成脚本：$generator"
    }

    Sync-OpenAIEnvironment

    $pythonVersion = & python --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "当前命令行不能运行 python。"
    }

    $apiKey = [Environment]::GetEnvironmentVariable("OPENAI_RELAY_API_KEY", "Process")
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        $apiKey = [Environment]::GetEnvironmentVariable("OPENAI_RELAY_API_KEY", "User")
    }
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        $apiKey = [Environment]::GetEnvironmentVariable("OPENAI_API_KEY", "Process")
    }
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        $apiKey = [Environment]::GetEnvironmentVariable("OPENAI_API_KEY", "User")
    }

    $app = Get-CorelDrawApp -Version $CorelVersion
    [pscustomobject]@{
        Python = $pythonVersion
        CorelDRAW = $app.Version
        OpenAIKey = if ([string]::IsNullOrWhiteSpace($apiKey)) { "missing" } else { "set" }
        Model = Get-VectorModel
        ApiUrl = Get-VectorApiUrl
        Timeout = Get-ApiTimeout
        Generator = $generator
    }
}

function Invoke-ArtGeneration {
    param(
        [string]$Prompt,
        [string]$Model,
        [string]$Style,
        [string]$Size,
        [string]$Colors,
        [string]$ReferenceImage,
        [bool]$AllowText
    )

    if ([string]::IsNullOrWhiteSpace($Prompt)) {
        throw "请输入图形描述。"
    }

    Sync-OpenAIEnvironment
    $model = $Model
    if ([string]::IsNullOrWhiteSpace($model)) {
        $model = Get-VectorModel
    }
    $apiUrl = Get-VectorApiUrl
    $apiTimeout = Get-ApiTimeout

    $outPath = Join-Path $env:TEMP ("gpt-cdr-vector-{0}.svg" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
    Add-PanelLog "读取配置完成。"
    Add-PanelLog "模型：$model"
    Add-PanelLog "接口：$apiUrl"
    Add-PanelLog "超时：$apiTimeout 秒"
    if (![string]::IsNullOrWhiteSpace($ReferenceImage)) {
        Add-PanelLog "参照图：$ReferenceImage"
    }
    Add-PanelLog "输出文件：$outPath"

    $args = @(
        $generator,
        $Prompt,
        "--output", $outPath,
        "--api-url", $apiUrl,
        "--model", $model,
        "--timeout", $apiTimeout,
        "--style", $Style,
        "--size", $Size
    )

    if (![string]::IsNullOrWhiteSpace($Colors)) {
        $args += @("--colors", $Colors.Trim())
    }
    if (![string]::IsNullOrWhiteSpace($ReferenceImage)) {
        $args += @("--reference-image", $ReferenceImage)
    }
    if ($AllowText) {
        $args += "--allow-text"
    }

    Add-PanelLog "启动 Python 生成脚本，开始等待模型返回。"
    $job = Start-Job -ScriptBlock {
        param([object[]]$PythonArgs)
        $output = & python @PythonArgs 2>&1
        [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output = ($output | Out-String).Trim()
        }
    } -ArgumentList (,$args)

    $startedAt = Get-Date
    $lastLoggedAt = -15
    while ($job.State -eq "Running") {
        $elapsed = [int]((Get-Date) - $startedAt).TotalSeconds
        if (($elapsed - $lastLoggedAt) -ge 15) {
            Add-PanelLog "模型生成中，已等待 $elapsed 秒。"
            $lastLoggedAt = $elapsed
        }
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 500
    }

    $result = Receive-Job -Job $job
    $jobState = $job.State
    $jobError = $job.ChildJobs[0].JobStateInfo.Reason
    Remove-Job -Job $job -Force

    if ($jobState -eq "Failed") {
        Add-PanelLog "Python 任务异常结束。"
        throw "SVG 生成失败：`r`n$jobError"
    }

    if ($result.ExitCode -ne 0) {
        $detail = $result.Output
        if ([string]::IsNullOrWhiteSpace($detail)) {
            $detail = "Python 未返回详细错误。"
        }
        Add-PanelLog "生成失败：Python 返回错误。"
        throw "SVG 生成失败：`r`n$detail"
    }
    if (!(Test-Path $outPath)) {
        Add-PanelLog "生成失败：没有找到输出文件。"
        throw "生成命令结束，但没有找到 SVG 输出：$outPath"
    }

    Add-PanelLog "SVG 生成完成。"
    return $outPath
}

function Import-ArtToCorelDraw {
    param([string]$FilePath)

    if (!(Test-Path $FilePath)) {
        throw "找不到文件：$FilePath"
    }

    Add-PanelLog "连接 CorelDRAW。"
    $app = Get-CorelDrawApp -Version $CorelVersion
    $doc = $null
    try { $doc = $app.ActiveDocument } catch {}
    if ($null -eq $doc) {
        Add-PanelLog "当前没有活动文档，创建新文档。"
        $doc = $app.CreateDocument()
    }

    Add-PanelLog "导入 SVG 到当前活动图层。"
    $options = $app.CreateStructImportOptions()
    $app.ActiveLayer.Import($FilePath, 0, $options) | Out-Null
    try { $app.Refresh() } catch {}
    Add-PanelLog "CorelDRAW 导入完成。"
}

if ($SelfTest) {
    Test-PanelEnvironment | Format-List
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$form = New-Object System.Windows.Forms.Form
$form.Text = "GPT CDR Vector - 中转 SVG 面板"
$form.Width = 560
$form.Height = 805
$form.StartPosition = "CenterScreen"
$form.TopMost = $false

$lblPrompt = New-Object System.Windows.Forms.Label
$lblPrompt.Text = "图形描述"
$lblPrompt.Left = 18
$lblPrompt.Top = 18
$lblPrompt.Width = 120
$form.Controls.Add($lblPrompt)

$txtPrompt = New-Object System.Windows.Forms.TextBox
$txtPrompt.Left = 18
$txtPrompt.Top = 45
$txtPrompt.Width = 505
$txtPrompt.Height = 155
$txtPrompt.Multiline = $true
$txtPrompt.ScrollBars = "Vertical"
$form.Controls.Add($txtPrompt)

$lblModel = New-Object System.Windows.Forms.Label
$lblModel.Text = "模型"
$lblModel.Left = 18
$lblModel.Top = 220
$lblModel.Width = 70
$form.Controls.Add($lblModel)

$cmbModel = New-Object System.Windows.Forms.ComboBox
$cmbModel.Left = 90
$cmbModel.Top = 216
$cmbModel.Width = 160
$cmbModel.DropDownStyle = "DropDownList"
@("gpt-5.4-mini", "gpt-5.4", "gpt-5.5", "gpt-4.1-mini") | ForEach-Object { [void]$cmbModel.Items.Add($_) }
$initialModel = Get-VectorModel
if (!$cmbModel.Items.Contains($initialModel)) {
    [void]$cmbModel.Items.Add($initialModel)
}
$cmbModel.SelectedItem = "gpt-5.4-mini"
$form.Controls.Add($cmbModel)

$lblStyle = New-Object System.Windows.Forms.Label
$lblStyle.Text = "风格"
$lblStyle.Left = 285
$lblStyle.Top = 220
$lblStyle.Width = 70
$form.Controls.Add($lblStyle)

$cmbStyle = New-Object System.Windows.Forms.ComboBox
$cmbStyle.Left = 355
$cmbStyle.Top = 216
$cmbStyle.Width = 168
$cmbStyle.DropDownStyle = "DropDownList"
@("poster", "illustration", "logo", "icon", "sticker", "line-art", "engraving", "pattern", "custom") | ForEach-Object { [void]$cmbStyle.Items.Add($_) }
$cmbStyle.SelectedItem = "poster"
$form.Controls.Add($cmbStyle)

$lblSize = New-Object System.Windows.Forms.Label
$lblSize.Text = "尺寸"
$lblSize.Left = 18
$lblSize.Top = 270
$lblSize.Width = 70
$form.Controls.Add($lblSize)

$cmbSize = New-Object System.Windows.Forms.ComboBox
$cmbSize.Left = 90
$cmbSize.Top = 266
$cmbSize.Width = 160
$cmbSize.DropDownStyle = "DropDownList"
@("1240x1754", "1080x1920", "1024x1024", "2048x2048", "1024x768", "768x1024") | ForEach-Object { [void]$cmbSize.Items.Add($_) }
$cmbSize.SelectedItem = "1240x1754"
$form.Controls.Add($cmbSize)

$lblColors = New-Object System.Windows.Forms.Label
$lblColors.Text = "颜色"
$lblColors.Left = 285
$lblColors.Top = 270
$lblColors.Width = 70
$form.Controls.Add($lblColors)

$txtColors = New-Object System.Windows.Forms.TextBox
$txtColors.Left = 355
$txtColors.Top = 266
$txtColors.Width = 168
$form.Controls.Add($txtColors)

$chkText = New-Object System.Windows.Forms.CheckBox
$chkText.Text = "允许 SVG 文字"
$chkText.Left = 90
$chkText.Top = 315
$chkText.Width = 180
$form.Controls.Add($chkText)

$lblRef = New-Object System.Windows.Forms.Label
$lblRef.Text = "参照图"
$lblRef.Left = 18
$lblRef.Top = 355
$lblRef.Width = 70
$form.Controls.Add($lblRef)

$txtRef = New-Object System.Windows.Forms.TextBox
$txtRef.Left = 90
$txtRef.Top = 351
$txtRef.Width = 285
$txtRef.ReadOnly = $true
$form.Controls.Add($txtRef)

$btnRef = New-Object System.Windows.Forms.Button
$btnRef.Text = "选择"
$btnRef.Left = 390
$btnRef.Top = 349
$btnRef.Width = 62
$form.Controls.Add($btnRef)

$btnClearRef = New-Object System.Windows.Forms.Button
$btnClearRef.Text = "清除"
$btnClearRef.Left = 461
$btnClearRef.Top = 349
$btnClearRef.Width = 62
$form.Controls.Add($btnClearRef)

$status = New-Object System.Windows.Forms.Label
$status.Left = 18
$status.Top = 397
$status.Width = 505
$status.Height = 45
$status.Text = "就绪：可选参照图，使用中转文本接口生成可编辑 SVG，再导入 CorelDRAW。"
$form.Controls.Add($status)

$lblLog = New-Object System.Windows.Forms.Label
$lblLog.Text = "运行日志"
$lblLog.Left = 18
$lblLog.Top = 453
$lblLog.Width = 100
$form.Controls.Add($lblLog)

$txtLog = New-Object System.Windows.Forms.TextBox
$txtLog.Left = 18
$txtLog.Top = 479
$txtLog.Width = 505
$txtLog.Height = 145
$txtLog.Multiline = $true
$txtLog.ReadOnly = $true
$txtLog.ScrollBars = "Vertical"
$form.Controls.Add($txtLog)
$script:LogBox = $txtLog

$btnTest = New-Object System.Windows.Forms.Button
$btnTest.Text = "测试连接"
$btnTest.Left = 18
$btnTest.Top = 642
$btnTest.Width = 105
$form.Controls.Add($btnTest)

$btnGenerate = New-Object System.Windows.Forms.Button
$btnGenerate.Text = "只生成 SVG"
$btnGenerate.Left = 140
$btnGenerate.Top = 642
$btnGenerate.Width = 115
$form.Controls.Add($btnGenerate)

$btnGenerateImport = New-Object System.Windows.Forms.Button
$btnGenerateImport.Text = "生成并导入"
$btnGenerateImport.Left = 272
$btnGenerateImport.Top = 642
$btnGenerateImport.Width = 115
$form.Controls.Add($btnGenerateImport)

$btnImport = New-Object System.Windows.Forms.Button
$btnImport.Text = "导入文件"
$btnImport.Left = 405
$btnImport.Top = 642
$btnImport.Width = 118
$form.Controls.Add($btnImport)

$btnClose = New-Object System.Windows.Forms.Button
$btnClose.Text = "关闭"
$btnClose.Left = 418
$btnClose.Top = 725
$btnClose.Width = 105
$form.Controls.Add($btnClose)

$btnTest.Add_Click({
    try {
        Clear-PanelLog
        Add-PanelLog "开始测试连接。"
        $result = Test-PanelEnvironment
        $status.Text = "连接正常：CorelDRAW $($result.CorelDRAW)，模型 $($result.Model)，API Key：$($result.OpenAIKey)"
        Add-PanelLog "Python：$($result.Python)"
        Add-PanelLog "CorelDRAW：$($result.CorelDRAW)"
        Add-PanelLog "模型：$($result.Model)"
        Add-PanelLog "面板选择模型：$($cmbModel.SelectedItem)"
        Add-PanelLog "接口：$($result.ApiUrl)"
        Add-PanelLog "超时：$($result.Timeout) 秒"
        Add-PanelLog "连接测试完成。"
    } catch {
        $status.Text = "连接失败：" + $_.Exception.Message
        Add-PanelLog "连接失败：$($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "GPT CDR Vector", "OK", "Warning") | Out-Null
    }
})

$btnRef.Add_Click({
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
    if ($dialog.ShowDialog() -eq "OK") {
        $script:ReferenceImagePath = $dialog.FileName
        $txtRef.Text = $dialog.FileName
        $status.Text = "已选择参照图：`r`n$($dialog.FileName)"
        Add-PanelLog "已选择参照图：$($dialog.FileName)"
    }
})

$btnClearRef.Add_Click({
    $script:ReferenceImagePath = ""
    $txtRef.Text = ""
    $status.Text = "已清除参照图。"
    Add-PanelLog "已清除参照图。"
})

$btnGenerate.Add_Click({
    try {
        Clear-PanelLog
        Add-PanelLog "任务开始：只生成 SVG。"
        $status.Text = "正在通过中转接口生成 SVG，复杂海报可能需要几分钟..."
        $form.Refresh()
        $file = Invoke-ArtGeneration -Prompt $txtPrompt.Text -Model $cmbModel.SelectedItem -Style $cmbStyle.SelectedItem -Size $cmbSize.SelectedItem -Colors $txtColors.Text -ReferenceImage $script:ReferenceImagePath -AllowText $chkText.Checked
        $status.Text = "已生成 SVG：`r`n$file"
        Add-PanelLog "任务完成：SVG 已生成。"
        [System.Windows.Forms.MessageBox]::Show("SVG 已生成：`r`n$file", "GPT CDR Vector", "OK", "Information") | Out-Null
    } catch {
        $status.Text = "生成失败：" + $_.Exception.Message
        Add-PanelLog "任务失败：$($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "GPT CDR Vector", "OK", "Warning") | Out-Null
    }
})

$btnGenerateImport.Add_Click({
    try {
        Clear-PanelLog
        Add-PanelLog "任务开始：生成并导入。"
        $status.Text = "正在通过中转接口生成 SVG，复杂海报可能需要几分钟..."
        $form.Refresh()
        $file = Invoke-ArtGeneration -Prompt $txtPrompt.Text -Model $cmbModel.SelectedItem -Style $cmbStyle.SelectedItem -Size $cmbSize.SelectedItem -Colors $txtColors.Text -ReferenceImage $script:ReferenceImagePath -AllowText $chkText.Checked
        $status.Text = "正在导入 CorelDRAW..."
        Add-PanelLog "开始导入 CorelDRAW。"
        $form.Refresh()
        Import-ArtToCorelDraw -FilePath $file
        $status.Text = "完成：SVG 已导入 CorelDRAW。`r`n$file"
        Add-PanelLog "任务完成：SVG 已生成并导入。"
    } catch {
        $status.Text = "失败：" + $_.Exception.Message
        Add-PanelLog "任务失败：$($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "GPT CDR Vector", "OK", "Warning") | Out-Null
    }
})

$btnImport.Add_Click({
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "SVG/Image files (*.svg;*.png;*.jpg;*.jpeg)|*.svg;*.png;*.jpg;*.jpeg|All files (*.*)|*.*"
    if ($dialog.ShowDialog() -eq "OK") {
        try {
            Clear-PanelLog
            Add-PanelLog "任务开始：导入已有文件。"
            Add-PanelLog "文件：$($dialog.FileName)"
            Import-ArtToCorelDraw -FilePath $dialog.FileName
            $status.Text = "完成：文件已导入 CorelDRAW。`r`n$($dialog.FileName)"
            Add-PanelLog "任务完成：文件已导入。"
        } catch {
            $status.Text = "导入失败：" + $_.Exception.Message
            Add-PanelLog "任务失败：$($_.Exception.Message)"
            [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "GPT CDR Vector", "OK", "Warning") | Out-Null
        }
    }
})

$btnClose.Add_Click({ $form.Close() })

[void]$form.ShowDialog()
