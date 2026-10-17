$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$brandPath = Join-Path $projectRoot 'assets\BrandMark.xaml'
$brandImage = [System.Windows.Markup.XamlReader]::Parse([System.IO.File]::ReadAllText($brandPath))
$brandImage.Freeze()
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
foreach ($size in ($sizes + 512)) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $drawing = $visual.RenderOpen()
    $drawing.DrawImage($brandImage, [System.Windows.Rect]::new(0, 0, $size, $size))
    $drawing.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $memory = New-Object System.IO.MemoryStream
    $encoder.Save($memory)
    $bytes = $memory.ToArray()
    $memory.Dispose()
    if ($size -ne 512) { $frames += [PSCustomObject]@{ Size = $size; Bytes = $bytes } }
    if ($size -eq 128 -or $size -eq 512) { [System.IO.File]::WriteAllBytes((Join-Path $projectRoot ('assets\spacio-' + $size + '.png')), $bytes) }
}
$iconPath = Join-Path $projectRoot 'assets\spacio.ico'
$iconStream = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($iconStream)
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frame.Bytes.Length)
        $writer.Write([UInt32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose() }
Write-Output 'Brand assets rendered: seven Windows icon sizes, 128px and 512px PNG.'
