Add-Type -AssemblyName System.Drawing

param(
    [string]$SourcePng = ""
)

$assetDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($SourcePng)) {
    $SourcePng = Join-Path $assetDirectory 'inputor.png'
}

$previewPath = Join-Path $assetDirectory 'inputor-preview.png'
$iconPath = Join-Path $assetDirectory 'inputor.ico'

if (-not (Test-Path $SourcePng)) {
    Write-Error "Source PNG not found: $SourcePng"
    exit 1
}

$source = [System.Drawing.Image]::FromFile($SourcePng)

function Resize-Bitmap {
    param([System.Drawing.Image]$Src, [int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.DrawImage($Src, 0, 0, $Size, $Size)
    $graphics.Dispose()
    return $bitmap
}

function Convert-BitmapToPngBytes {
    param([System.Drawing.Bitmap]$Bitmap)
    $stream = New-Object System.IO.MemoryStream
    try {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

function Write-IcoFile {
    param(
        [string]$Path,
        [byte[][]]$PngImages,
        [int[]]$Sizes
    )

    $file = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    $writer = New-Object System.IO.BinaryWriter $file
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$PngImages.Length)

        $offset = 6 + (16 * $PngImages.Length)
        for ($i = 0; $i -lt $PngImages.Length; $i++) {
            $sz = $Sizes[$i]
            $data = $PngImages[$i]
            $writer.Write([byte]([math]::Min($sz, 255) % 256))
            $writer.Write([byte]([math]::Min($sz, 255) % 256))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$data.Length)
            $writer.Write([UInt32]$offset)
            $offset += $data.Length
        }
        foreach ($data in $PngImages) {
            $writer.Write($data)
        }
    }
    finally {
        $writer.Dispose()
        $file.Dispose()
    }
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$pngImages = New-Object 'System.Collections.Generic.List[byte[]]'

foreach ($size in $sizes) {
    $bitmap = Resize-Bitmap -Src $source -Size $size
    try {
        $pngImages.Add((Convert-BitmapToPngBytes -Bitmap $bitmap))
        if ($size -eq 256) {
            $bitmap.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
            Write-Host "Saved preview: $previewPath"
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

$source.Dispose()

Write-IcoFile -Path $iconPath -PngImages $pngImages.ToArray() -Sizes $sizes
Write-Host "Saved icon: $iconPath"
Write-Host "Done. Sizes: $($sizes -join ', ')px"
