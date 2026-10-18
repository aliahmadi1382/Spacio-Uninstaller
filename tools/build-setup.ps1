param(
    [string]$Executable = (Join-Path $PSScriptRoot '..\dist\Spacio.exe'),
    [string]$Package,
    [string]$Destination = (Join-Path $PSScriptRoot '..\dist')
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$exePath = [IO.Path]::GetFullPath($Executable)
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($exePath).Version
$version = $assemblyVersion.ToString(3)
if (-not $Package) { $Package = Join-Path $projectRoot ('dist\Spacio-' + $version + '-win-x64.zip') }
$packagePath = (Resolve-Path -LiteralPath $Package).Path
$outputDirectory = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$stage = Join-Path $projectRoot ('.test-data\setup-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
$hashes = @()
try {
    foreach ($name in @('Spacio.exe', 'Spacio.exe.config', 'README.md', 'CHANGELOG.md')) {
        $entries = @($archive.Entries | Where-Object { $_.FullName -ceq $name })
        if ($entries.Count -ne 1) { throw ('Invalid package entry: ' + $name) }
        $path = Join-Path $stage $name
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entries[0], $path)
        $hashes += $name + '=' + (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
} finally { $archive.Dispose() }
if ((Get-FileHash $exePath).Hash -ne (Get-FileHash (Join-Path $stage 'Spacio.exe')).Hash) { throw 'Package executable differs from the tested build.' }
$hashes | Set-Content -LiteralPath (Join-Path $stage 'payload.txt') -Encoding ASCII
@"
using System.Reflection;
[assembly: AssemblyTitle("Spacio Setup")]
[assembly: AssemblyProduct("Spacio")]
[assembly: AssemblyDescription("Per-user Spacio installation and removal")]
[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$assemblyVersion")]
"@ | Set-Content -LiteralPath (Join-Path $stage 'Version.cs') -Encoding UTF8
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$setupPath = Join-Path $outputDirectory ('Spacio-' + $version + '-Setup.exe')
$compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+','/langversion:5','/utf8output',('/out:' + $setupPath),('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')),('/win32icon:' + (Join-Path $projectRoot 'assets\spacio.ico')),
    '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.IO.Compression.dll',
    ('/resource:' + $packagePath + ',Spacio.Package.zip'),('/resource:' + (Join-Path $stage 'payload.txt') + ',Spacio.Payload.txt'),
    ('/resource:' + (Join-Path $projectRoot 'assets\spacio.ico') + ',Spacio.ico'),('/resource:' + (Join-Path $projectRoot 'assets\spacio-128.png') + ',Spacio.png'),
    (Join-Path $stage 'Version.cs'),(Join-Path $projectRoot 'src\InstallationRegistration.cs'))
$compilerArgs += (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'installer') -Filter '*.cs').FullName
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Setup compilation failed.' }
# Keep the ZIP used by the existing in-app updater and include both artifacts in checksums.
$checksumPath = Join-Path $outputDirectory ('Spacio-' + $version + '-SHA256SUMS.txt')
$checksumLines = @($packagePath, $setupPath) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
[IO.File]::WriteAllLines($checksumPath, $checksumLines, [Text.Encoding]::ASCII)
Write-Output $setupPath
Write-Output $checksumPath
