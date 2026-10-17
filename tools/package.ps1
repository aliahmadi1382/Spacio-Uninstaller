param([string]$Executable = (Join-Path $PSScriptRoot '..\dist\Spacio.exe'))
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$exePath = [System.IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $exePath) -or -not (Test-Path -LiteralPath ($exePath + '.config'))) { throw 'Build Spacio before packaging it.' }
$version = ([version][System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).FileVersion).ToString(3)
$outputDirectory = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$zipPath = Join-Path $outputDirectory ('Spacio-' + $version + '-win-x64.zip')
# The updater expects these exact executable/config names at the ZIP root.
Compress-Archive -LiteralPath @($exePath, ($exePath + '.config'), (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'CHANGELOG.md')) -DestinationPath $zipPath -Force
$hashPath = Join-Path $outputDirectory ('Spacio-' + $version + '-SHA256SUMS.txt')
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText($hashPath, ($hash + '  ' + [System.IO.Path]::GetFileName($zipPath) + "`n"), [System.Text.Encoding]::ASCII)
Write-Output $zipPath
Write-Output $hashPath
