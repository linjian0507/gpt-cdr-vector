param(
    [Parameter(Mandatory = $true)]
    [string]$AddonsRoot,
    [string]$PackageDir,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($PackageDir)) {
    $PackageDir = Join-Path $repoRoot "dist\coreldraw_addon\gpt-cdr-vector"
}

$appExe = Join-Path $PackageDir "app.exe"
if (!(Test-Path $appExe)) {
    & (Join-Path $PSScriptRoot "build_coreldraw_addon.ps1")
}
if (!(Test-Path $appExe)) {
    throw "Cannot find built app.exe: $appExe"
}

if (!(Test-Path $AddonsRoot)) {
    throw "Addons root does not exist: $AddonsRoot"
}

$targetDir = Join-Path $AddonsRoot "gpt-cdr-vector"
if ((Test-Path $targetDir) -and !$Force) {
    throw "Target directory already exists: $targetDir. Use -Force to overwrite files."
}

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
Copy-Item -Path (Join-Path $PackageDir "*") -Destination $targetDir -Recurse -Force

Write-Host "Installed GPT CDR Vector Addon to:"
Write-Host $targetDir
Write-Host "Restart CorelDRAW. The GPT CDR Vector toolbar should appear at the top."
