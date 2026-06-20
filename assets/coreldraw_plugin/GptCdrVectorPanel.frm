VERSION 5.00
Object = "{0D452EE1-E08F-101A-852E-02608C4D0BB4}#2.0#0"; "FM20.DLL"
Begin VB.UserForm GptCdrVectorPanel
   Caption         =   "GPT CDR Vector"
   ClientHeight    =   6825
   ClientLeft      =   120
   ClientTop       =   465
   ClientWidth     =   6120
   StartUpPosition =   1
   Begin MSForms.CommandButton cmdClose
      Caption         =   "关闭"
      Height          =   360
      Left            =   4800
      TabIndex        =   12
      Top             =   6240
      Width           =   960
   End
   Begin MSForms.CommandButton cmdImport
      Caption         =   "导入已有 SVG"
      Height          =   420
      Left            =   3120
      TabIndex        =   11
      Top             =   5640
      Width           =   1680
   End
   Begin MSForms.CommandButton cmdGenerateImport
      Caption         =   "生成并导入"
      Height          =   420
      Left            =   1320
      TabIndex        =   10
      Top             =   5640
      Width           =   1560
   End
   Begin MSForms.CommandButton cmdGenerate
      Caption         =   "只生成 SVG"
      Height          =   420
      Left            =   240
      TabIndex        =   9
      Top             =   5640
      Width           =   960
   End
   Begin MSForms.Label lblStatus
      Caption         =   "就绪"
      Height          =   720
      Left            =   240
      TabIndex        =   8
      Top             =   4800
      Width           =   5520
   End
   Begin MSForms.CheckBox chkAllowText
      Caption         =   "允许 SVG 文字"
      Height          =   300
      Left            =   240
      TabIndex        =   7
      Top             =   4320
      Width           =   1800
   End
   Begin MSForms.TextBox txtColors
      Height          =   360
      Left            =   1320
      TabIndex        =   6
      Top             =   3840
      Width           =   4440
   End
   Begin MSForms.Label lblColors
      Caption         =   "颜色"
      Height          =   300
      Left            =   240
      TabIndex        =   5
      Top             =   3900
      Width           =   720
   End
   Begin MSForms.ComboBox cmbSize
      Height          =   360
      Left            =   4200
      TabIndex        =   4
      Top             =   3300
      Width           =   1560
   End
   Begin MSForms.Label lblSize
      Caption         =   "尺寸"
      Height          =   300
      Left            =   3480
      TabIndex        =   3
      Top             =   3360
      Width           =   720
   End
   Begin MSForms.ComboBox cmbStyle
      Height          =   360
      Left            =   1320
      TabIndex        =   2
      Top             =   3300
      Width           =   1800
   End
   Begin MSForms.Label lblStyle
      Caption         =   "风格"
      Height          =   300
      Left            =   240
      TabIndex        =   1
      Top             =   3360
      Width           =   720
   End
   Begin MSForms.TextBox txtPrompt
      Height          =   2460
      Left            =   240
      MultiLine       =   -1
      TabIndex        =   0
      Top             =   600
      Width           =   5520
   End
   Begin MSForms.Label lblPrompt
      Caption         =   "图形描述"
      Height          =   300
      Left            =   240
      TabIndex        =   13
      Top             =   240
      Width           =   1200
   End
End
Attribute VB_Name = "GptCdrVectorPanel"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Option Explicit

Private Sub UserForm_Initialize()
    cmbStyle.Clear
    cmbStyle.AddItem "illustration"
    cmbStyle.AddItem "logo"
    cmbStyle.AddItem "icon"
    cmbStyle.AddItem "sticker"
    cmbStyle.AddItem "line-art"
    cmbStyle.AddItem "engraving"
    cmbStyle.AddItem "pattern"
    cmbStyle.AddItem "custom"
    cmbStyle.Value = "illustration"

    cmbSize.Clear
    cmbSize.AddItem "1024x1024"
    cmbSize.AddItem "2048x2048"
    cmbSize.AddItem "1024x768"
    cmbSize.AddItem "768x1024"
    cmbSize.Value = "1024x1024"

    txtColors.Text = ""
    chkAllowText.Value = False
    lblStatus.Caption = "就绪：输入描述后可生成 SVG，或直接生成并导入当前文档。"
End Sub

Private Sub cmdGenerate_Click()
    RunGenerate False
End Sub

Private Sub cmdGenerateImport_Click()
    RunGenerate True
End Sub

Private Sub cmdImport_Click()
    GptCdrVectorPlugin.ImportGeneratedSvgFile
End Sub

Private Sub cmdClose_Click()
    Unload Me
End Sub

Private Sub RunGenerate(ByVal importAfterGenerate As Boolean)
    On Error GoTo Failed
    lblStatus.Caption = "正在生成，请稍候..."
    Repaint

    Dim svgPath As String
    svgPath = GptCdrVectorPlugin.GenerateSvg(txtPrompt.Text, cmbStyle.Value, cmbSize.Value, txtColors.Text, CBool(chkAllowText.Value))

    If importAfterGenerate Then
        lblStatus.Caption = "已生成，正在导入..."
        Repaint
        GptCdrVectorPlugin.ImportSvgFile svgPath
        lblStatus.Caption = "完成：SVG 已生成并导入。" & vbCrLf & svgPath
    Else
        lblStatus.Caption = "完成：SVG 已生成。" & vbCrLf & svgPath
        MsgBox "SVG 已生成：" & vbCrLf & svgPath, vbInformation, "GPT CDR Vector"
    End If
    Exit Sub

Failed:
    lblStatus.Caption = "失败：" & Err.Description
    MsgBox Err.Description, vbExclamation, "GPT CDR Vector"
End Sub
