param([string]$Destination = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkPath 'csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework 4.8 and its C# compiler are required.' }
$outputDirectory = [System.IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
& (Join-Path $projectRoot 'tools\build-brand.ps1')
$references = @('System.dll', 'System.Core.dll', 'System.Xaml.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll')
$compilerArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/langversion:5', '/utf8output', ('/out:' + (Join-Path $outputDirectory 'Spacio.exe')), ('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')), ('/resource:' + (Join-Path $projectRoot 'src\MainWindow.xaml') + ',Spacio.MainWindow.xaml'))
$compilerArgs += '/win32icon:' + (Join-Path $projectRoot 'assets\spacio.ico')
$compilerArgs += '/resource:' + (Join-Path $projectRoot 'assets\BrandMark.xaml') + ',Spacio.BrandMark.xaml'
$compilerArgs += '/resource:' + (Join-Path $projectRoot 'assets\spacio.ico') + ',Spacio.ico'
foreach ($reference in $references) { $compilerArgs += '/reference:' + (Join-Path $frameworkPath $reference) }
$compilerArgs += Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compilerPath @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\App.config') -Destination (Join-Path $outputDirectory 'Spacio.exe.config') -Force
Write-Output ('Built: ' + (Join-Path $outputDirectory 'Spacio.exe'))
