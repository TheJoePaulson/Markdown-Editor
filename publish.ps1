<#
.SYNOPSIS
    Builds, publishes, README-stamps, and zips the Markdown Editor as a
    portable WinUI 3 self-contained app.

.DESCRIPTION
    One-shot release pipeline:
      1. Cleans bin/ and obj/
      2. Runs dotnet publish for win-x64 self-contained
      3. Writes a versioned README.txt into the publish folder
      4. Compresses the publish folder into a versioned ZIP

.PARAMETER Version
    Semantic version of this build (e.g. 1.0.0). Defaults to 1.0.0.

.PARAMETER OutputFolder
    Folder where the ZIP file is written. Defaults to D:\Releases.

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -Version 1.1.0
    .\publish.ps1 -Version 1.2.0 -OutputFolder "D:\Builds"
#>

param(
    [string]$Version = "1.0.0",
    [string]$OutputFolder = "D:\Releases"
)

# ----------------------------------------------------------------------
# Configuration
# ----------------------------------------------------------------------

$ErrorActionPreference = "Stop"
$projectRoot   = $PSScriptRoot
$projectFile   = Join-Path $projectRoot "MarkdownEditor.csproj"
$tfm           = "net8.0-windows10.0.19041.0"
$rid           = "win-x64"
$config        = "Release"
$platform      = "x64"

$publishPath   = Join-Path $projectRoot "bin\$platform\$config\$tfm\$rid\publish"
$dateTag       = Get-Date -Format "yyyyMMdd"
$dateReadable  = Get-Date -Format "yyyy-MM-dd"
$zipName       = "MarkdownEditor-Portable-v$Version-$dateTag.zip"
$zipPath       = Join-Path $OutputFolder $zipName

# ----------------------------------------------------------------------
# Banner
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "========================================================"
Write-Host " Markdown Editor - Portable Publish"
Write-Host "========================================================"
Write-Host " Version          : $Version"
Write-Host " Date             : $dateReadable"
Write-Host " Project          : $projectFile"
Write-Host " Publish folder   : $publishPath"
Write-Host " Output ZIP       : $zipPath"
Write-Host "========================================================"
Write-Host ""

# ----------------------------------------------------------------------
# 1. Clean
# ----------------------------------------------------------------------

Write-Host "==> Cleaning bin and obj..."
Remove-Item -Recurse -Force `
    (Join-Path $projectRoot "bin"), `
    (Join-Path $projectRoot "obj") `
    -ErrorAction SilentlyContinue

# ----------------------------------------------------------------------
# 2. Publish
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "==> Publishing version $Version (self-contained, $rid)..."
dotnet publish $projectFile `
    -c $config `
    -r $rid `
    --self-contained true `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:SelfContained=true `
    -p:UseRidGraph=true `
    -p:WindowsAppSdkUndockedRegFreeWinRTInitialize=true `
    -p:Version=$Version `
    -p:FileVersion="$Version.0" `
    -p:AssemblyVersion="$Version.0" `
	-p:SatelliteResourceLanguages=en-US

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
    exit 1
}

if (-not (Test-Path $publishPath)) {
    Write-Error "Publish folder not found at expected path: $publishPath"
    exit 1
}

# ----------------------------------------------------------------------
# 3. Verify critical files are present
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "==> Verifying publish output..."

$expectedFiles = @(
    "MarkdownEditor.exe",
    "MarkdownEditor.dll",
    "MarkdownEditor.pri",
    "App.xbf",
    "MainWindow.xbf",
    "Microsoft.UI.Xaml.dll",
    "Microsoft.WindowsAppRuntime.dll",
    "Microsoft.WindowsAppRuntime.Bootstrap.dll",
    "Microsoft.Web.WebView2.Core.dll",
    "WebView2Loader.dll",
    "resources.pri",
    "Markdig.dll"
)

$missing = @()
foreach ($file in $expectedFiles) {
    $full = Join-Path $publishPath $file
    if (-not (Test-Path $full)) {
        $missing += $file
    }
}

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Warning "The following expected files are missing from publish output:"
    foreach ($m in $missing) {
        Write-Warning "  - $m"
    }
    Write-Warning "The build may still run, but distribution is at risk."
} else {
    Write-Host "All expected files are present."
}

# ----------------------------------------------------------------------
# 4. Write README.txt
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "==> Writing README.txt..."

$readme = @"
Markdown Editor - Portable v$Version
Built: $dateReadable

How to use
----------
  1. Unzip this folder anywhere (USB drive, network share, or local disk).
  2. Double-click MarkdownEditor.exe.
  3. No installation required.

Storage
-------
  Your settings, autosaves, backups, and logs are stored alongside the EXE
  under the per-user profile folder:

      Data\Profiles\<your-username>\

  Move or copy the entire folder and your work travels with you.

Requirements
------------
  - Windows 11 (any edition)
  - Or Windows Server 2025 with Desktop Experience

Features in this build
----------------------
  - Live Markdown preview (Markdig + WebView2)
  - Open / Save / Save As (.md, .markdown, .txt)
  - Recent Files dropdown on the Open button
  - Light / Dark / System theme switcher
  - Window size and position persistence
  - Atomic file saves with timestamped backups
  - Crash-safe autosave with restore on launch
  - Per-user profile isolation
  - Structured rotating logs
  - Read-only share fallback to %LOCALAPPDATA%

Author : Joe Paulson
Build  : Self-contained, unpackaged WinUI 3 (.NET 8 - $rid)
"@

$readmePath = Join-Path $publishPath "README.txt"
Set-Content -Path $readmePath -Value $readme -Encoding UTF8

Write-Host "README written to $readmePath"

# ----------------------------------------------------------------------
# 5. Ensure output folder exists
# ----------------------------------------------------------------------

if (-not (Test-Path $OutputFolder)) {
    Write-Host ""
    Write-Host "==> Creating output folder: $OutputFolder"
    New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
}

# ----------------------------------------------------------------------
# 6. Create versioned ZIP
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "==> Creating ZIP..."

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive `
    -Path "$publishPath\*" `
    -DestinationPath $zipPath `
    -Force

$sizeBytes = (Get-Item $zipPath).Length
$sizeMb    = [Math]::Round($sizeBytes / 1MB, 1)

# ----------------------------------------------------------------------
# Done
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "========================================================"
Write-Host " Publish complete"
Write-Host "========================================================"
Write-Host " Version : $Version"
Write-Host " ZIP     : $zipPath"
Write-Host " Size    : $sizeMb MB"
Write-Host "========================================================"
Write-Host ""