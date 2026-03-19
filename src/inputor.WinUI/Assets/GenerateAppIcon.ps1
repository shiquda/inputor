Add-Type -AssemblyName System.Drawing

$assetDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$previewPath = Join-Path $assetDirectory 'inputor-preview.png'
$iconPath = Join-Path $assetDirectory 'inputor.ico'

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-ShieldPath {
    param([float]$Size)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.StartFigure()
    $path.AddLine($Size * 0.50, $Size * 0.17, $Size * 0.73, $Size * 0.27)
    $path.AddLine($Size * 0.73, $Size * 0.27, $Size * 0.68, $Size * 0.58)
    $path.AddBezier(
        $Size * 0.68, $Size * 0.58,
        $Size * 0.66, $Size * 0.72,
        $Size * 0.58, $Size * 0.83,
        $Size * 0.50, $Size * 0.89)
    $path.AddBezier(
        $Size * 0.50, $Size * 0.89,
        $Size * 0.42, $Size * 0.83,
        $Size * 0.34, $Size * 0.72,
        $Size * 0.32, $Size * 0.58)
    $path.AddLine($Size * 0.32, $Size * 0.58, $Size * 0.27, $Size * 0.27)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $canvas = New-Object System.Drawing.RectangleF ($Size * 0.08), ($Size * 0.08), ($Size * 0.84), ($Size * 0.84)
    $radius = $Size * 0.22
    $surfacePath = New-RoundedRectanglePath -Rect $canvas -Radius $radius
    $surfaceBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        ([System.Drawing.PointF]::new(0, 0)),
        ([System.Drawing.PointF]::new($Size, $Size)),
        ([System.Drawing.Color]::FromArgb(255, 14, 28, 47)),
        ([System.Drawing.Color]::FromArgb(255, 24, 93, 149)))
    $graphics.FillPath($surfaceBrush, $surfacePath)

    $highlightBrush = $null
    if ($Size -gt 20) {
        $highlightBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(42, 129, 227, 255))
        $graphics.FillEllipse($highlightBrush, $Size * 0.14, $Size * 0.08, $Size * 0.56, $Size * 0.36)
    }

    $shieldPath = New-ShieldPath -Size $Size
    $shieldBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(242, 244, 249, 255))
    $graphics.FillPath($shieldBrush, $shieldPath)

    $barBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        ([System.Drawing.PointF]::new($Size * 0.37, $Size * 0.35)),
        ([System.Drawing.PointF]::new($Size * 0.63, $Size * 0.70)),
        ([System.Drawing.Color]::FromArgb(255, 103, 238, 217)),
        ([System.Drawing.Color]::FromArgb(255, 26, 173, 224)))

    $barCorner = [math]::Max(1, $Size * 0.03)
    $bars = if ($Size -le 20) {
        @(
            @{ X = 0.385; Y = 0.51; Width = 0.08; Height = 0.16 },
            @{ X = 0.475; Y = 0.42; Width = 0.08; Height = 0.25 },
            @{ X = 0.565; Y = 0.33; Width = 0.08; Height = 0.34 }
        )
    }
    else {
        @(
            @{ X = 0.385; Y = 0.53; Width = 0.07; Height = 0.14 },
            @{ X = 0.472; Y = 0.44; Width = 0.07; Height = 0.23 },
            @{ X = 0.559; Y = 0.35; Width = 0.07; Height = 0.32 }
        )
    }

    foreach ($bar in $bars) {
        $barRect = New-Object System.Drawing.RectangleF ($Size * $bar.X), ($Size * $bar.Y), ($Size * $bar.Width), ($Size * $bar.Height)
        $barPath = New-RoundedRectanglePath -Rect $barRect -Radius $barCorner
        $graphics.FillPath($barBrush, $barPath)
        $barPath.Dispose()
    }

    $accentPen = $null
    if ($Size -gt 20) {
        $accentPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(88, 255, 255, 255), [math]::Max(1, $Size * 0.018))
        $graphics.DrawArc($accentPen, $Size * 0.21, $Size * 0.18, $Size * 0.18, $Size * 0.18, 210, 110)
    }

    $surfacePath.Dispose()
    $surfaceBrush.Dispose()
    if ($highlightBrush) { $highlightBrush.Dispose() }
    $shieldPath.Dispose()
    $shieldBrush.Dispose()
    $barBrush.Dispose()
    if ($accentPen) { $accentPen.Dispose() }
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
        for ($index = 0; $index -lt $PngImages.Length; $index++) {
            $size = $Sizes[$index]
            $data = $PngImages[$index]
            $writer.Write([byte]([math]::Min($size, 255) % 256))
            $writer.Write([byte]([math]::Min($size, 255) % 256))
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
    $bitmap = New-IconBitmap -Size $size
    try {
        $pngImages.Add((Convert-BitmapToPngBytes -Bitmap $bitmap))
        if ($size -eq 256) {
            $bitmap.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

Write-IcoFile -Path $iconPath -PngImages $pngImages.ToArray() -Sizes $sizes
