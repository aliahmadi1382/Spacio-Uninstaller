param(
    [Parameter(Mandatory = $true)][string]$Setup,
    [Parameter(Mandatory = $true)][string]$ReferenceExecutable,
    [switch]$PublicUpgrade
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'artifacts') -Force | Out-Null
$setupPath = (Resolve-Path -LiteralPath $Setup).Path
$referencePath = (Resolve-Path -LiteralPath $ReferenceExecutable).Path
$harness = Join-Path $projectRoot ('.test-data\setup-probe-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $harness -Force | Out-Null
Copy-Item -LiteralPath $referencePath -Destination (Join-Path $harness 'Spacio.exe')
Copy-Item -LiteralPath ($referencePath + '.config') -Destination (Join-Path $harness 'Spacio.exe.config')
$probe = Join-Path $harness 'UpgradeProbe.exe'
Copy-Item -LiteralPath ($referencePath + '.config') -Destination ($probe + '.config')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /langversion:5 ('/out:' + $probe) ('/reference:' + (Join-Path $harness 'Spacio.exe')) (Join-Path $PSScriptRoot 'UpgradeProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Setup update probe compilation failed.' }
$report = Join-Path $projectRoot ('artifacts\setup-' + $(if ($PublicUpgrade) { 'public-' } else { '' }) + [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '.txt')
$arguments = @('--self-test', ('"' + $report + '"'), ('"' + $probe + '"'))
if ($PublicUpgrade) { $arguments += '--public' }
$process = Start-Process -FilePath $setupPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(90000)) { throw ('Setup tests still running. Inspect ' + $report + ' and process ' + $process.Id) }
Get-Content -LiteralPath $report
if ($process.ExitCode -ne 0) { throw ('Setup verification failed: ' + $report) }
Write-Output ('Report: ' + $report)
