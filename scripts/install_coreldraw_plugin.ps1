param(
    [string]$CorelVersion = "24",
    [switch]$ListTargets
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$pluginSource = Join-Path $repoRoot "assets\coreldraw_plugin"
$moduleFile = Join-Path $pluginSource "GptCdrVectorPlugin.bas"
$formFile = Join-Path $pluginSource "GptCdrVectorPanel.frm"

if (!(Test-Path $moduleFile) -or !(Test-Path $formFile)) {
    throw "Plugin source files are missing under $pluginSource"
}

function Get-CorelComTarget {
    param([string]$Version)

    $before = @(Get-Process -Name CorelDRW -ErrorAction SilentlyContinue)
    $beforeIds = @($before | ForEach-Object { $_.Id })
    $app = $null
    try {
        $app = New-Object -ComObject "CorelDRAW.Application.$Version"
        [pscustomobject]@{
            Version = $Version
            Label = "CorelDRAW $($app.Version)"
            UserGMSPath = $app.GMSManager.UserGMSPath
        }
    } finally {
        $after = @(Get-Process -Name CorelDRW -ErrorAction SilentlyContinue)
        $newProcesses = @($after | Where-Object { $beforeIds -notcontains $_.Id })
        if ($null -ne $app -and $newProcesses.Count -gt 0) {
            try { $app.Quit() } catch {}
        }
    }
}

$targets = @()
foreach ($id in @("24", "20", "14")) {
    try {
        $targets += Get-CorelComTarget -Version $id
    } catch {
    }
}

if ($ListTargets) {
    $targets | Format-Table -AutoSize
    exit 0
}

$target = $targets | Where-Object { $_.Version -eq $CorelVersion } | Select-Object -First 1
if ($null -eq $target) {
    if ($targets.Count -eq 1) {
        $target = $targets[0]
    } else {
        throw "CorelDRAW version $CorelVersion was not found. Run this script with -ListTargets."
    }
}

$installDir = Join-Path $target.UserGMSPath "gpt-cdr-vector-plugin-source"
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Force -Path $moduleFile, $formFile -Destination $installDir

[Environment]::SetEnvironmentVariable("GPT_CDR_VECTOR_SKILL_DIR", $repoRoot.Path, "User")

$readme = @"
GPT CDR Vector plugin source has been prepared for $($target.Label).

Source folder:
$installDir

Manual import in CorelDRAW:
1. Restart CorelDRAW.
2. Open Tools > Scripts > Scripts, or press Alt+Shift+F11.
3. Create a new VBA macro project in the user GMS folder, named GptCdrVectorPlugin.gms.
4. In the Script Editor / Visual Basic Editor, import:
   - $installDir\GptCdrVectorPlugin.bas
   - $installDir\GptCdrVectorPanel.frm
5. Run:
   GptCdrVectorPlugin.ShowGptCdrVectorPanel
6. Optional: add that macro to a toolbar from CorelDRAW customization settings.

Environment variable set:
GPT_CDR_VECTOR_SKILL_DIR=$($repoRoot.Path)
"@

$readmePath = Join-Path $installDir "README_IMPORT.txt"
Set-Content -Path $readmePath -Value $readme -Encoding UTF8

Write-Host "Prepared CorelDRAW plugin source for $($target.Label)"
Write-Host "User GMS path: $($target.UserGMSPath)"
Write-Host "Source folder: $installDir"
Write-Host "Read next: $readmePath"
