param(
    [string]$OutputDir,
    [switch]$Zip
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sourceFile = Join-Path $repoRoot "assets\coreldraw_addon\GptCdrVectorAddon.cs"
$toolbarSource = Join-Path $repoRoot "assets\coreldraw_addon\GptCdrVectorToolbar.cs"
$installerSource = Join-Path $repoRoot "assets\coreldraw_addon\GptCdrVectorInstaller.cs"
$settingsSource = Join-Path $repoRoot "assets\coreldraw_addon\GptCdrVectorApiSettings.cs"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "dist\coreldraw_addon\gpt-cdr-vector"
}

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (!(Test-Path $csc)) {
    throw "Cannot find .NET Framework csc.exe."
}

$frameworkDir = Split-Path -Parent $csc
$wpfDir = Join-Path $frameworkDir "WPF"
$windowsBase = Join-Path $wpfDir "WindowsBase.dll"
$presentationCore = Join-Path $wpfDir "PresentationCore.dll"
$presentationFramework = Join-Path $wpfDir "PresentationFramework.dll"
$systemXaml = Join-Path $frameworkDir "System.Xaml.dll"
foreach ($reference in @($windowsBase, $presentationCore, $presentationFramework, $systemXaml)) {
    if (!(Test-Path $reference)) {
        throw "Cannot find required WPF reference: $reference"
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$distRoot = Split-Path -Parent $OutputDir

$appExe = Join-Path $OutputDir "app.exe"
$hostDll = Join-Path $OutputDir "GptCdrVectorHost.dll"
$installerExe = Join-Path $distRoot "GptCdrVectorInstaller.exe"
$embeddedPackageZip = Join-Path $distRoot "gpt-cdr-vector-embedded.zip"
& $csc /nologo /target:winexe /platform:anycpu /out:$appExe `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll `
    /reference:Microsoft.CSharp.dll `
    $sourceFile `
    $settingsSource
if ($LASTEXITCODE -ne 0) {
    throw "Failed to compile app.exe."
}

& $csc /nologo /target:library /platform:anycpu /out:$hostDll `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:$windowsBase `
    /reference:$presentationCore `
    /reference:$presentationFramework `
    /reference:$systemXaml `
    $toolbarSource
if ($LASTEXITCODE -ne 0) {
    throw "Failed to compile GptCdrVectorHost.dll."
}

[System.IO.File]::WriteAllBytes((Join-Path $OutputDir "CorelDrw.addon"), [byte[]]@())

$appUi = @'
<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:frmwrk="Corel Framework Data">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>

  <frmwrk:uiconfig>
    <frmwrk:applicationInfo userConfiguration="true" />
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/items">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <itemData guid="96d6b822-8d2c-4077-8f8f-d1ce0b9896e5"
                type="wpfhost"
                hostedType="Addons\gpt-cdr-vector\GptCdrVectorHost.dll,GptCdrVectorHost.Toolbar"
                enable="true">
      </itemData>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/commandBars">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <commandBarData guid="9f52c93f-d4a5-4317-961d-1d271a0eb211"
                      nonLocalizableName="gpt-cdr-vector"
                      userCaption="GPT CDR Vector"
                      locked="false"
                      type="toolbar">
        <toolbar>
          <item guidRef="96d6b822-8d2c-4077-8f8f-d1ce0b9896e5" dock="top"/>
        </toolbar>
      </commandBarData>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/containers/container[@guid='bee85f91-3ad9-dc8d-48b5-d2a87c8b2109']/container[@guid='Framework_MainFrame-layout']/dockHost[@guid='894bf987-2ec1-8f83-41d8-68f6797d0db4']/toolbar[@guidRef='c2b44f69-6dec-444e-a37e-5dbf7ff43dae']">
    <xsl:copy-of select="."/>
    <toolbar guidRef="9f52c93f-d4a5-4317-961d-1d271a0eb211" dock="top" />
  </xsl:template>
</xsl:stylesheet>
'@
Set-Content -LiteralPath (Join-Path $OutputDir "AppUI.xslt") -Value $appUi -Encoding UTF8

$userUi = @'
<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:frmwrk="Corel Framework Data" exclude-result-prefixes="frmwrk">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>
  <frmwrk:uiconfig>
    <frmwrk:applicationInfo userConfiguration="true" />
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>
'@
Set-Content -LiteralPath (Join-Path $OutputDir "UserUI.xslt") -Value $userUi -Encoding UTF8

$configJson = @'
{
  "name": "GPT CDR Vector",
  "entry": "app.exe",
  "toolbarHost": "GptCdrVectorHost.dll",
  "description": "Generate editable SVG vector artwork through a relay model and import it into CorelDRAW.",
  "corelProgIdDefault": "CorelDRAW.Application.20",
  "apiKeyEnv": ["OPENAI_RELAY_API_KEY", "OPENAI_API_KEY"],
  "timeoutEnv": "OPENAI_API_TIMEOUT",
  "corelVersionEnv": "GPT_CDR_VECTOR_COREL_VERSION"
}
'@
Set-Content -LiteralPath (Join-Path $OutputDir "config.json") -Value $configJson -Encoding UTF8

$presetsJson = @'
[
  {
    "name": "No preset",
    "prompt": "",
    "description": "Follow the user brief directly as editable SVG."
  },
  {
    "name": "Icon button",
    "prompt": "Generate one centered SVG icon with consistent strokes, clear silhouette, and small-size readability.",
    "description": "For icon/logotype-like SVG workflows."
  },
  {
    "name": "Logo wordmark",
    "prompt": "Generate a clean logo or wordmark SVG with balanced negative space, stable proportions, and simple geometry.",
    "description": "For brand marks, letter marks, and shop marks."
  },
  {
    "name": "Single object",
    "prompt": "Generate one isolated object SVG with no unrelated background and editable grouped parts.",
    "description": "For objects, products, and character subjects."
  },
  {
    "name": "Reference to SVG",
    "prompt": "Rebuild the reference image as native editable SVG shapes without embedding raster data.",
    "description": "For image-to-SVG workflows."
  },
  {
    "name": "Subject extraction",
    "prompt": "Extract the main subject from the prompt or reference image into a clean editable SVG subject layer.",
    "description": "For isolating subjects and reusable graphic elements."
  },
  {
    "name": "Line art",
    "prompt": "Generate clean line-art SVG with consistent strokes, minimal fills, and clear contours.",
    "description": "For outlines, drawings, and instructional illustration."
  },
  {
    "name": "Cut engraving",
    "prompt": "Generate SVG for cutting or engraving with closed paths, minimal overlaps, and single-color priority.",
    "description": "For plotters, laser engraving, and contour cutting."
  },
  {
    "name": "Sticker badge",
    "prompt": "Generate sticker or badge SVG with bold contour, clear border, and print-friendly flat colors.",
    "description": "For stickers, badges, and patches."
  },
  {
    "name": "Product label",
    "prompt": "Generate product label SVG with border, ornaments, icons, and editable text areas.",
    "description": "For package labels, hang tags, and price tags."
  },
  {
    "name": "Editable poster",
    "prompt": "Generate a complete poster SVG with layout hierarchy, title area, key visual, and decorative detail.",
    "description": "For campaign posters and activity artwork."
  },
  {
    "name": "Infographic",
    "prompt": "Generate infographic SVG with sections, icons, labels, arrows, or simple charts.",
    "description": "For visual explanations and data graphics."
  },
  {
    "name": "Diagram",
    "prompt": "Generate flowchart or structural diagram SVG with aligned nodes, connectors, arrows, and labels.",
    "description": "For diagram-to-SVG workflows."
  },
  {
    "name": "Seamless pattern",
    "prompt": "Generate repeat-friendly SVG pattern with consistent spacing and tileable edges when relevant.",
    "description": "For background motifs and package patterns."
  },
  {
    "name": "Background texture",
    "prompt": "Generate vector background SVG with abstract shapes, texture, gradients, and ornaments.",
    "description": "For decorative backgrounds and page foundations."
  }
]
'@
Set-Content -LiteralPath (Join-Path $OutputDir "msc.json") -Value $presetsJson -Encoding UTF8

$uiSettings = @'
AI=Bar
Vector=Bar
Import=Bar
Settings=Menu
'@
Set-Content -LiteralPath (Join-Path $OutputDir "uisettings.ini") -Value $uiSettings -Encoding UTF8

$readme = @'
GPT CDR Vector - CorelDRAW Addons package

Files:
- CorelDrw.addon: marker file that lets CorelDRAW scan this Addons folder.
- AppUI.xslt: adds a fixed top toolbar hosted control to the CorelDRAW workspace.
- GptCdrVectorHost.dll: small WPF toolbar button host. Clicking it opens app.exe.
- app.exe: main non-VBA panel. It calls the relay API directly and imports SVG into CorelDRAW through COM.
- config.json: package metadata and environment variable names.
- msc.json: preset descriptions for humans and future UI extension.
- uisettings.ini: simple Addons-style grouping reference.

Install:
1. Prefer running the single-file GptCdrVectorInstaller.exe. It embeds this package, can auto-detect CorelDRAW Addons roots, and installs without needing the zip beside it.
2. Manual install fallback: keep CorelDRAW closed.
3. Copy this whole "gpt-cdr-vector" folder to your CorelDRAW Addons root, for example:
   <CorelDRAW>\Programs64\Addons\gpt-cdr-vector
4. Start CorelDRAW.
5. A "GPT CDR Vector" toolbar should appear at the top with a button.
6. Click the toolbar button to open the panel. You can also run app.exe directly from this folder.

If the toolbar does not appear:
- Make sure the folder name is exactly "gpt-cdr-vector" directly under Programs64\Addons.
- Restart CorelDRAW.
- If the workspace was already cached, start CorelDRAW while holding F8 to reapply workspace UI transforms.

Required environment variables:
- OPENAI_RELAY_API_KEY: your relay API key.

API settings:
- Run GptCdrVectorInstaller.exe and click the settings button, or open app.exe and click settings.
- The settings page writes OPENAI_RELAY_API_KEY, OPENAI_VECTOR_API_URL, OPENAI_VECTOR_MODEL, and OPENAI_API_TIMEOUT to the current Windows user environment.

Optional environment variables:
- OPENAI_API_TIMEOUT: request timeout in seconds, default 600.
- GPT_CDR_VECTOR_COREL_VERSION: CorelDRAW COM version, default 20 for CorelDRAW 2018.

Notes:
- This package does not require VBA.
- It does not require Python for generation.
- It does not modify any existing Addons folder unless you copy or install it yourself.
'@
Set-Content -LiteralPath (Join-Path $OutputDir "README_INSTALL.txt") -Value $readme -Encoding UTF8

if (Test-Path $embeddedPackageZip) {
    Remove-Item -LiteralPath $embeddedPackageZip -Force
}
Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $embeddedPackageZip

& $csc /nologo /target:winexe /platform:anycpu /out:$installerExe `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.IO.Compression.FileSystem.dll `
    /resource:$embeddedPackageZip,GptCdrVectorPackage.zip `
    $installerSource `
    $settingsSource
if ($LASTEXITCODE -ne 0) {
    throw "Failed to compile GptCdrVectorInstaller.exe."
}
Remove-Item -LiteralPath $embeddedPackageZip -Force

if ($Zip) {
    $zipPath = Join-Path $distRoot "gpt-cdr-vector-addon.zip"
    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path $OutputDir, $installerExe -DestinationPath $zipPath
    Write-Host "Zip package: $zipPath"
}

Write-Host "Addon package: $OutputDir"
Write-Host "App: $appExe"
Write-Host "Toolbar host: $hostDll"
Write-Host "Installer: $installerExe"
