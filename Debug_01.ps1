$build   = "D:\Source\Repos\MarkdownEditor\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64"
$publish = "D:\Source\Repos\MarkdownEditor\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"

Write-Host "BUILD folder has:"
Test-Path "$build\Microsoft.UI.Xaml.Controls"
Get-ChildItem "$build\Microsoft.UI.Xaml.Controls" -ErrorAction SilentlyContinue | Measure-Object | Select-Object Count

Write-Host ""
Write-Host "PUBLISH folder has:"
Test-Path "$publish\Microsoft.UI.Xaml.Controls"
Get-ChildItem "$publish\Microsoft.UI.Xaml.Controls" -ErrorAction SilentlyContinue | Measure-Object | Select-Object Count
