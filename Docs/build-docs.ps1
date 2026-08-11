<#
.SYNOPSIS
    Converts UserGuide.md to UserGuide.html using Markdig.
    Uses the same theme colors as the in-app preview for consistency.

.EXAMPLE
    .\build-docs.ps1
#>

param(
    [string]$InputMd = "$PSScriptRoot\UserGuide.md",
    [string]$OutputHtml = "$PSScriptRoot\UserGuide.html"
)

$ErrorActionPreference = "Stop"

# Find Markdig.dll in the publish or debug build output.
$possiblePaths = @(
    "$PSScriptRoot\..\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\Markdig.dll",
    "$PSScriptRoot\..\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\Markdig.dll",
    "$PSScriptRoot\..\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\Markdig.dll"
)

$markdigDll = $possiblePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $markdigDll) {
    Write-Error "Could not find Markdig.dll. Build the project first."
    exit 1
}

Write-Host "Using Markdig from: $markdigDll"
Add-Type -Path $markdigDll

if (-not (Test-Path $InputMd)) {
    Write-Error "Input file not found: $InputMd"
    exit 1
}

$markdown = Get-Content -Path $InputMd -Raw -Encoding UTF8

# Build a pipeline with advanced extensions (matches the in-app preview).
$pipeline = [Markdig.MarkdownPipelineBuilder]::new()
[Markdig.MarkDownExtensions]::UseAdvancedExtensions($pipeline) | Out-Null
$builtPipeline = $pipeline.Build()

$body = [Markdig.Markdown]::ToHtml($markdown, $builtPipeline)

$css = @"
body {
  font-family: 'Segoe UI', Arial, sans-serif;
  font-size: 15px;
  line-height: 1.6;
  color: #222;
  background: #ffffff;
  max-width: 900px;
  margin: 40px auto;
  padding: 20px 40px;
}
h1, h2, h3, h4 {
  color: #0F4E92;
  margin-top: 1.5em;
}
h1 { border-bottom: 2px solid #0F4E92; padding-bottom: 6px; }
h2 { border-bottom: 1px solid #dddddd; padding-bottom: 4px; }
code {
  font-family: Consolas, 'Courier New', monospace;
  background: #f3f3f3;
  padding: 2px 4px;
  border-radius: 3px;
  font-size: 0.9em;
}
pre {
  background: #f6f8fa;
  padding: 12px;
  border-radius: 6px;
  overflow-x: auto;
}
pre code {
  background: none;
  padding: 0;
}
blockquote {
  border-left: 4px solid #0F4E92;
  margin: 12px 0;
  padding: 8px 16px;
  color: #555;
  background: #f9f9f9;
}
table {
  border-collapse: collapse;
  margin: 12px 0;
  width: 100%;
}
th, td {
  border: 1px solid #dddddd;
  padding: 8px 12px;
  text-align: left;
}
th {
  background: #f3f3f3;
  font-weight: 600;
}
a {
  color: #0F4E92;
}
hr {
  border: none;
  border-top: 1px solid #dddddd;
  margin: 24px 0;
}
"@

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>Markdown Editor - User Guide</title>
  <style>$css</style>
</head>
<body>
$body
</body>
</html>
"@

Set-Content -Path $OutputHtml -Value $html -Encoding UTF8

$sizeKb = [math]::Round((Get-Item $OutputHtml).Length / 1KB, 1)
Write-Host ""
Write-Host "Generated: $OutputHtml"
Write-Host "Size:      $sizeKb KB"