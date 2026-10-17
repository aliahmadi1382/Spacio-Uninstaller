param([string]$Executable = (Join-Path $PSScriptRoot 'dist\Spacio.exe'))
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$exePath = [System.IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $exePath)) { & (Join-Path $projectRoot 'build.ps1') -Destination (Split-Path -Parent $exePath) }
$artifactPath = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
$reportPath = Join-Path $artifactPath 'tests.txt'
$testProcess = Start-Process -FilePath $exePath -ArgumentList @('--self-test', ('"' + $reportPath + '"')) -WindowStyle Hidden -PassThru -Wait
Get-Content -LiteralPath $reportPath
if ($testProcess.ExitCode -ne 0) { throw 'Core checks failed. See artifacts\tests.txt.' }
$cacheReport = Join-Path $artifactPath 'cache-tests.txt'
$cacheProcess = Start-Process -FilePath $exePath -ArgumentList @('--cache-test', ('"' + $cacheReport + '"')) -WindowStyle Hidden -PassThru -Wait
Get-Content -LiteralPath $cacheReport
if ($cacheProcess.ExitCode -ne 0) { throw 'Cache checks failed.' }
$liveReport = Join-Path $artifactPath 'live-tests.txt'
$liveProcess = Start-Process -FilePath $exePath -ArgumentList @('--live-test', ('"' + $liveReport + '"')) -WindowStyle Hidden -PassThru
if (-not $liveProcess.WaitForExit(120000)) { throw 'Live checks exceeded 120 seconds.' }
Get-Content -LiteralPath $liveReport
if ($liveProcess.ExitCode -ne 0) { throw 'Live checks failed.' }
$updateReport = Join-Path $artifactPath 'update-tests.txt'
$updateProcess = Start-Process -FilePath $exePath -ArgumentList @('--update-test', ('"' + $updateReport + '"')) -WindowStyle Hidden -PassThru
if (-not $updateProcess.WaitForExit(60000)) { throw 'Update checks exceeded 60 seconds.' }
Get-Content -LiteralPath $updateReport
if ($updateProcess.ExitCode -ne 0) { throw 'Update checks failed.' }
$fixturePath = (Get-Content -LiteralPath $reportPath | Where-Object { $_ -like 'FIXTURE=*' }).Substring(8)
$previewPrefix = Join-Path $artifactPath ('ui-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss'))
$uiProcess = Start-Process -FilePath $exePath -ArgumentList @('--ui-smoke', ('"' + $fixturePath + '"'), ('"' + $previewPrefix + '"')) -WindowStyle Hidden -PassThru
if (-not $uiProcess.WaitForExit(60000)) { throw ('UI checks exceeded 60 seconds. Inspect process ' + $uiProcess.Id + '.') }
if ((Test-Path -LiteralPath ($previewPrefix + '.error.txt')) -or -not (Test-Path -LiteralPath ($previewPrefix + '.ok.txt'))) { throw ('UI checks failed. Inspect ' + $previewPrefix + '.error.txt') }
Get-Content -LiteralPath ($previewPrefix + '.ok.txt')
$reopenProcess = Start-Process -FilePath $exePath -ArgumentList @('--ui-smoke', ('"' + $fixturePath + '"'), ('"' + $previewPrefix + '"'), '--reopen') -WindowStyle Hidden -PassThru
if (-not $reopenProcess.WaitForExit(60000)) { throw 'Cache restart check exceeded 60 seconds.' }
if ($reopenProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath ($previewPrefix + '.reopen.ok.txt'))) { throw 'Cache restart check failed.' }
Get-Content -LiteralPath ($previewPrefix + '.reopen.ok.txt')
$startupProcess = Start-Process -FilePath $exePath -ArgumentList @('--startup-smoke', ('"' + $previewPrefix + '-cache"'), ('"' + $previewPrefix + '"')) -WindowStyle Hidden -PassThru
if (-not $startupProcess.WaitForExit(60000)) { throw 'Startup check exceeded 60 seconds.' }
if ($startupProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath ($previewPrefix + '.startup.ok.txt'))) { throw 'Loading-screen startup check failed.' }
Get-Content -LiteralPath ($previewPrefix + '.startup.ok.txt')
$motionProcess = Start-Process -FilePath $exePath -ArgumentList @('--loading-smoke', ('"' + $previewPrefix + '"')) -WindowStyle Hidden -PassThru
if (-not $motionProcess.WaitForExit(30000)) { throw 'Live loading animation check exceeded 30 seconds.' }
if ($motionProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath ($previewPrefix + '.motion.txt'))) { throw ('Live animation check failed. Inspect ' + $previewPrefix + '.motion.error.txt') }
Get-Content -LiteralPath ($previewPrefix + '.motion.txt')
Write-Output ('Screenshots: ' + $previewPrefix + '-en.png and -fa.png')
