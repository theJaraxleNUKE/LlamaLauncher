<#
    Publishes a self-contained Windows build and (optionally) compiles the
    Inno Setup installer.

    Usage (from the repo root, in PowerShell):
        ./packaging/windows/build-windows.ps1
        ./packaging/windows/build-windows.ps1 -Version 1.2.0 -Runtime win-x64

    Prereqs:
        * .NET 8 SDK
        * Inno Setup 6+ (for the installer step): https://jrsoftware.org/isdl.php
          ISCC.exe must be on PATH, or edit $iscc below.
#>

param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64",
    [string]$Version       = "1.0.0",
    [switch]$SingleFile,          # also emit a portable single-file exe
    [switch]$SkipInstaller        # publish only, don't run Inno Setup
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$project  = Join-Path $repoRoot "LlamaLauncher.csproj"
$publish  = Join-Path $repoRoot "installer\windows\publish"

Write-Host "==> Publishing $Runtime ($Configuration) ..." -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    -o $publish

if ($SingleFile) {
    $portable = Join-Path $repoRoot "dist"
    Write-Host "==> Publishing portable single-file exe ..." -ForegroundColor Cyan
    dotnet publish $project `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:Version=$Version `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $portable
    Write-Host "    Portable exe: $portable\LlamaLauncher.exe"
}

if ($SkipInstaller) { Write-Host "Done (installer skipped)."; return }

# Locate ISCC (Inno Setup command-line compiler).
$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue)?.Source
if (-not $iscc) {
    foreach ($p in @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe")) {
        if (Test-Path $p) { $iscc = $p; break }
    }
}
if (-not $iscc) {
    Write-Warning "Inno Setup (ISCC.exe) not found. Published folder is at $publish."
    Write-Warning "Install Inno Setup 6 from https://jrsoftware.org/isdl.php then re-run, or run -SkipInstaller."
    return
}

$iss = Join-Path $repoRoot "installer\windows\LlamaLauncher.iss"
Write-Host "==> Compiling installer with $iscc ..." -ForegroundColor Cyan
& $iscc "/DMyAppVersion=$Version" $iss

Write-Host "Done. Installer is in installer\windows\Output." -ForegroundColor Green
