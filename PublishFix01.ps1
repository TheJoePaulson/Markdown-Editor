$build   = "D:\Source\Repos\MarkdownEditor\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64"
$publish = "D:\Source\Repos\MarkdownEditor\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"

Copy-Item -Path "$build\Microsoft.UI.Xaml.Controls" `
          -Destination "$publish\Microsoft.UI.Xaml.Controls" `
          -Recurse -Force

Write-Host "Copy complete."