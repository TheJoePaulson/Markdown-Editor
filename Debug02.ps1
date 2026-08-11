$debug   = "D:\Source\Repos\MarkdownEditor\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64"
$publish = "D:\Source\Repos\MarkdownEditor\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"

$debugFiles = Get-ChildItem -Path $debug -File | Select-Object -ExpandProperty Name
$publishFiles = Get-ChildItem -Path $publish -File | Select-Object -ExpandProperty Name

Write-Host "Files in DEBUG but NOT in PUBLISH:"
Write-Host "---------------------------------"
$debugFiles | Where-Object { $publishFiles -notcontains $_ } | Sort-Object