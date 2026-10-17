param([Parameter(Mandatory = $true)][string]$PreviousExecutable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$oldExePath = [System.IO.Path]::GetFullPath($PreviousExecutable)
if (-not (Test-Path -LiteralPath $oldExePath) -or -not (Test-Path -LiteralPath ($oldExePath + '.config'))) { throw 'Provide an extracted Spacio release from 1.3.0 or later.' }
$area = Join-Path $projectRoot ('.test-data\public-upgrade-' + [Guid]::NewGuid().ToString('N'))
$harnessDirectory = Join-Path $area 'harness'
$targetDirectory = Join-Path $area 'installation'
New-Item -ItemType Directory -Path $harnessDirectory, $targetDirectory -Force | Out-Null
foreach ($directory in @($harnessDirectory, $targetDirectory)) {
    Copy-Item -LiteralPath $oldExePath -Destination (Join-Path $directory 'Spacio.exe')
    Copy-Item -LiteralPath ($oldExePath + '.config') -Destination (Join-Path $directory 'Spacio.exe.config')
}
$probe = Join-Path $harnessDirectory 'UpgradeProbe.exe'
Copy-Item -LiteralPath ($oldExePath + '.config') -Destination ($probe + '.config')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:5 ('/out:' + $probe) ('/reference:' + (Join-Path $harnessDirectory 'Spacio.exe')) (Join-Path $PSScriptRoot 'UpgradeProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Upgrade probe compilation failed.' }
$report = Join-Path $area 'report.txt'
$process = Start-Process -FilePath $probe -ArgumentList @(('"' + (Join-Path $targetDirectory 'Spacio.exe') + '"'), ('"' + $report + '"')) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { throw ('Live upgrade still running; inspect process ' + $process.Id + ' and ' + $area) }
Get-Content -LiteralPath $report
if ($process.ExitCode -ne 0) { throw ('Live upgrade failed. See ' + $report) }
Write-Output ('Report: ' + $report)
