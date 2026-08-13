param(
    [string]$SourcePath = (Join-Path $PSScriptRoot 'Assets\fmt-app-icon-source.png'),
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function New-LogoBitmap {
    param(
        [System.Drawing.Bitmap]$Source,
        [int]$Size,
        [bool]$MarkOnly
    )

    $canvas = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $canvas.SetResolution(96, 96)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        if ($MarkOnly) {
            # The propeller mark ends at x=109; x=110 begins the FMT wordmark.
            $markWidth = [Math]::Min(110, $Source.Width)
            $sourceRect = New-Object System.Drawing.Rectangle(0, 0, $markWidth, $Source.Height)
            $fillRatio = 0.86
        }
        else {
            $sourceRect = New-Object System.Drawing.Rectangle(0, 0, $Source.Width, $Source.Height)
            $fillRatio = 0.90
        }

        $scale = [Math]::Min(
            ($Size * $fillRatio) / $sourceRect.Width,
            ($Size * $fillRatio) / $sourceRect.Height)
        $width = [Math]::Max(1, [int][Math]::Round($sourceRect.Width * $scale))
        $height = [Math]::Max(1, [int][Math]::Round($sourceRect.Height * $scale))
        $x = [int](($Size - $width) / 2)
        $y = [int](($Size - $height) / 2)
        $destination = New-Object System.Drawing.Rectangle($x, $y, $width, $height)

        $attributes = New-Object System.Drawing.Imaging.ImageAttributes
        try {
            $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $graphics.DrawImage(
                $Source,
                $destination,
                $sourceRect.X,
                $sourceRect.Y,
                $sourceRect.Width,
                $sourceRect.Height,
                [System.Drawing.GraphicsUnit]::Pixel,
                $attributes)
        }
        finally {
            $attributes.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    return $canvas
}

function Convert-BitmapToPngBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $stream = New-Object System.IO.MemoryStream
    try {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        # Prevent PowerShell from unrolling the byte array into pipeline items.
        return ,$stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

function Write-MultiSizeIcon {
    param(
        [System.Drawing.Bitmap]$Source,
        [string]$OutputPath
    )

    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $frames = @()

    foreach ($size in $sizes) {
        $bitmap = New-LogoBitmap -Source $Source -Size $size -MarkOnly ($size -le 64)
        try {
            $frames += ,(Convert-BitmapToPngBytes -Bitmap $bitmap)
        }
        finally {
            $bitmap.Dispose()
        }
    }

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$sizes.Count)

        $offset = 6 + (16 * $sizes.Count)
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $size = $sizes[$index]
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$frames[$index].Length)
            $writer.Write([UInt32]$offset)
            $offset += $frames[$index].Length
        }

        foreach ($frame in $frames) {
            $writer.Write([byte[]]$frame)
        }

        [System.IO.File]::WriteAllBytes($OutputPath, $stream.ToArray())
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$source = [System.Drawing.Bitmap]::FromFile($SourcePath)
try {
    Write-MultiSizeIcon -Source $source -OutputPath (Join-Path $ProjectRoot 'mpdesktop.ico')

    $small = New-LogoBitmap -Source $source -Size 44 -MarkOnly $true
    try {
        $small.Save((Join-Path $ProjectRoot 'mpdesktop44.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $small.Dispose()
    }

    $large = New-LogoBitmap -Source $source -Size 150 -MarkOnly $false
    try {
        $large.Save((Join-Path $ProjectRoot 'mpdesktop150.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $large.Dispose()
    }
}
finally {
    $source.Dispose()
}

Write-Output 'Generated mpdesktop.ico, mpdesktop44.png, and mpdesktop150.png.'
