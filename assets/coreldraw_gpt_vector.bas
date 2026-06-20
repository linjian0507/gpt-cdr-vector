Attribute VB_Name = "GptCdrVector"
Option Explicit

Private Function QuoteForPowerShell(ByVal value As String) As String
    QuoteForPowerShell = "'" & Replace(value, "'", "''") & "'"
End Function

Private Function QuoteForCmd(ByVal value As String) As String
    QuoteForCmd = """" & Replace(value, """", """""") & """"
End Function

Private Function EnvValue(ByVal name As String) As String
    EnvValue = CreateObject("WScript.Shell").Environment("PROCESS")(name)
End Function

Private Function SkillDir() As String
    Dim value As String
    value = EnvValue("GPT_CDR_VECTOR_SKILL_DIR")
    If Len(value) = 0 Then
        value = InputBox("Path to the gpt-cdr-vector skill folder:", "GPT CDR Vector")
    End If
    SkillDir = value
End Function

Public Sub GptVectorGenerateAndImport()
    Dim prompt As String
    prompt = InputBox("Describe the vector artwork to generate:", "GPT CDR Vector")
    If Len(prompt) = 0 Then Exit Sub

    Dim root As String
    root = SkillDir()
    If Len(root) = 0 Then Exit Sub

    Dim scriptPath As String
    scriptPath = root & "\scripts\generate_cdr_svg.py"

    Dim outPath As String
    outPath = EnvValue("TEMP") & "\gpt-cdr-vector.svg"

    Dim styleName As String
    styleName = InputBox("Style preset: logo, icon, sticker, illustration, line-art, engraving, pattern, custom", "GPT CDR Vector", "illustration")
    If Len(styleName) = 0 Then styleName = "illustration"

    Dim ps As String
    ps = "& python " & QuoteForPowerShell(scriptPath) & _
         " --output " & QuoteForPowerShell(outPath) & _
         " --style " & QuoteForPowerShell(styleName) & _
         " " & QuoteForPowerShell(prompt)

    Dim command As String
    command = "powershell -NoProfile -ExecutionPolicy Bypass -Command " & QuoteForCmd(ps)

    Dim exitCode As Long
    exitCode = CreateObject("WScript.Shell").Run(command, 1, True)
    If exitCode <> 0 Then
        MsgBox "SVG generation failed. Check OPENAI_API_KEY, Python, and the skill path.", vbExclamation, "GPT CDR Vector"
        Exit Sub
    End If

    ImportSvgFile outPath
End Sub

Public Sub ImportGeneratedSvgFile()
    Dim svgPath As String
    svgPath = InputBox("SVG file path to import:", "GPT CDR Vector")
    If Len(svgPath) = 0 Then Exit Sub
    ImportSvgFile svgPath
End Sub

Private Sub ImportSvgFile(ByVal svgPath As String)
    If Len(Dir(svgPath)) = 0 Then
        MsgBox "SVG not found: " & svgPath, vbExclamation, "GPT CDR Vector"
        Exit Sub
    End If

    On Error GoTo ImportFailed
    ActiveLayer.Import svgPath
    MsgBox "Imported SVG:" & vbCrLf & svgPath, vbInformation, "GPT CDR Vector"
    Exit Sub

ImportFailed:
    MsgBox "CorelDRAW could not import this SVG. Try importing it manually or simplify the SVG." & vbCrLf & Err.Description, vbExclamation, "GPT CDR Vector"
End Sub
