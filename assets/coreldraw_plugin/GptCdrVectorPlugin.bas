Attribute VB_Name = "GptCdrVectorPlugin"
Option Explicit

Private Const DEFAULT_SIZE As String = "1024x1024"
Private Const DEFAULT_STYLE As String = "illustration"

Public Sub ShowGptCdrVectorPanel()
    On Error GoTo Failed
    GptCdrVectorPanel.Show vbModeless
    Exit Sub

Failed:
    MsgBox "GPT CDR Vector panel could not be opened." & vbCrLf & Err.Description, vbExclamation, "GPT CDR Vector"
End Sub

Public Sub GptVectorGenerateAndImport()
    Dim prompt As String
    prompt = InputBox("Describe the vector artwork to generate:", "GPT CDR Vector")
    If Len(Trim$(prompt)) = 0 Then Exit Sub

    Dim styleName As String
    styleName = InputBox("Style preset: logo, icon, sticker, illustration, line-art, engraving, pattern, custom", "GPT CDR Vector", DEFAULT_STYLE)
    If Len(Trim$(styleName)) = 0 Then styleName = DEFAULT_STYLE

    On Error GoTo Failed
    Dim svgPath As String
    svgPath = GenerateSvg(prompt, styleName, DEFAULT_SIZE, "", False)
    ImportSvgFile svgPath
    Exit Sub

Failed:
    MsgBox Err.Description, vbExclamation, "GPT CDR Vector"
End Sub

Public Sub ImportGeneratedSvgFile()
    Dim svgPath As String
    svgPath = InputBox("SVG file path to import:", "GPT CDR Vector")
    If Len(Trim$(svgPath)) = 0 Then Exit Sub
    ImportSvgFile svgPath
End Sub

Public Function GenerateSvg(ByVal prompt As String, ByVal styleName As String, ByVal sizeName As String, ByVal colors As String, ByVal allowText As Boolean) As String
    prompt = Trim$(prompt)
    If Len(prompt) = 0 Then Err.Raise vbObjectError + 100, "GPT CDR Vector", "请输入图形描述。"

    Dim root As String
    root = SkillDir()
    If Len(root) = 0 Then Err.Raise vbObjectError + 101, "GPT CDR Vector", "未设置技能目录。"

    Dim scriptPath As String
    scriptPath = root & "\scripts\generate_cdr_svg.py"
    If Len(Dir(scriptPath)) = 0 Then Err.Raise vbObjectError + 102, "GPT CDR Vector", "找不到生成脚本：" & scriptPath

    styleName = NormalizeStyle(styleName)
    sizeName = NormalizeSize(sizeName)

    Dim outPath As String
    outPath = EnvValue("TEMP") & "\gpt-cdr-vector-" & Format$(Now, "yyyymmdd-hhnnss") & ".svg"

    Dim ps As String
    ps = "& python " & QuoteForPowerShell(scriptPath) & _
         " --output " & QuoteForPowerShell(outPath) & _
         " --style " & QuoteForPowerShell(styleName) & _
         " --size " & QuoteForPowerShell(sizeName)

    If Len(Trim$(colors)) > 0 Then
        ps = ps & " --colors " & QuoteForPowerShell(Trim$(colors))
    End If

    If allowText Then
        ps = ps & " --allow-text"
    End If

    ps = ps & " " & QuoteForPowerShell(prompt)

    Dim command As String
    command = "powershell -NoProfile -ExecutionPolicy Bypass -Command " & QuoteForCmd(ps)

    Dim exitCode As Long
    exitCode = CreateObject("WScript.Shell").Run(command, 1, True)
    If exitCode <> 0 Then
        Err.Raise vbObjectError + 103, "GPT CDR Vector", "SVG 生成失败。请检查 OPENAI_API_KEY、Python 和技能目录。"
    End If

    If Len(Dir(outPath)) = 0 Then
        Err.Raise vbObjectError + 104, "GPT CDR Vector", "生成命令结束但没有找到 SVG 输出：" & outPath
    End If

    GenerateSvg = outPath
End Function

Public Sub ImportSvgFile(ByVal svgPath As String)
    svgPath = Trim$(svgPath)
    If Len(svgPath) = 0 Then Exit Sub
    If Len(Dir(svgPath)) = 0 Then
        MsgBox "SVG not found:" & vbCrLf & svgPath, vbExclamation, "GPT CDR Vector"
        Exit Sub
    End If

    On Error GoTo ImportFailed
    Dim doc As Object
    On Error Resume Next
    Set doc = ActiveDocument
    On Error GoTo ImportFailed
    If doc Is Nothing Then
        Set doc = Application.CreateDocument
    End If
    ActiveLayer.Import svgPath
    MsgBox "已导入 SVG：" & vbCrLf & svgPath, vbInformation, "GPT CDR Vector"
    Exit Sub

ImportFailed:
    MsgBox "CorelDRAW 无法导入此 SVG，请尝试手动导入或简化 SVG。" & vbCrLf & Err.Description, vbExclamation, "GPT CDR Vector"
End Sub

Public Function SkillDir() As String
    Dim value As String
    value = EnvValue("GPT_CDR_VECTOR_SKILL_DIR")
    If Len(value) = 0 Then
        value = InputBox("请输入 gpt-cdr-vector 技能目录：", "GPT CDR Vector")
    End If
    SkillDir = Trim$(value)
End Function

Public Function EnvValue(ByVal name As String) As String
    EnvValue = CreateObject("WScript.Shell").Environment("PROCESS")(name)
End Function

Private Function NormalizeStyle(ByVal value As String) As String
    value = LCase$(Trim$(value))
    Select Case value
        Case "logo", "icon", "sticker", "illustration", "line-art", "engraving", "pattern", "custom"
            NormalizeStyle = value
        Case Else
            NormalizeStyle = DEFAULT_STYLE
    End Select
End Function

Private Function NormalizeSize(ByVal value As String) As String
    value = LCase$(Replace(Trim$(value), " ", ""))
    Dim pos As Long
    pos = InStr(1, value, "x", vbTextCompare)
    If pos > 1 And pos < Len(value) Then
        If IsSizePart(Left$(value, pos - 1)) And IsSizePart(Mid$(value, pos + 1)) Then
            NormalizeSize = value
            Exit Function
        End If
    End If
    NormalizeSize = DEFAULT_SIZE
End Function

Private Function IsSizePart(ByVal value As String) As Boolean
    If Len(value) < 2 Or Len(value) > 5 Then Exit Function
    If value Like "*[!0-9]*" Then Exit Function
    If CLng(value) < 10 Then Exit Function
    IsSizePart = True
End Function

Private Function QuoteForPowerShell(ByVal value As String) As String
    QuoteForPowerShell = "'" & Replace(value, "'", "''") & "'"
End Function

Private Function QuoteForCmd(ByVal value As String) As String
    QuoteForCmd = """" & Replace(value, """", """""") & """"
End Function
