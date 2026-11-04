param([Parameter(Mandatory = $true)][string]$OutputPath)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Render each size from the same mark rather than resizing a large bitmap.
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = foreach ($size in $sizes) {
    $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $shape = [Drawing.Drawing2D.GraphicsPath]::new()
    $fill = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(35, 105, 155))
    $stroke = [Drawing.Pen]::new([Drawing.Color]::White, 2.65)
    $png = [IO.MemoryStream]::new()
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.ScaleTransform($size / 32.0, $size / 32.0)
        $shape.AddArc(1, 1, 16, 16, 180, 90)
        $shape.AddArc(15, 1, 16, 16, 270, 90)
        $shape.AddArc(15, 15, 16, 16, 0, 90)
        $shape.AddArc(1, 15, 16, 16, 90, 90)
        $shape.CloseFigure()
        $graphics.FillPath($fill, $shape)
        $stroke.StartCap = [Drawing.Drawing2D.LineCap]::Round
        $stroke.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $stroke.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
        $points = [Drawing.PointF[]]@(
            [Drawing.PointF]::new(8, 22)
            [Drawing.PointF]::new(14, 15.5)
            [Drawing.PointF]::new(19, 20)
            [Drawing.PointF]::new(24, 11)
        )
        $graphics.DrawLines($stroke, $points)
        $bitmap.Save($png, [Drawing.Imaging.ImageFormat]::Png)
        [pscustomobject]@{ Size = $size; Data = $png.ToArray() }
    } finally {
        $png.Dispose()
        $stroke.Dispose()
        $fill.Dispose()
        $shape.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$writer = [IO.BinaryWriter]::new([IO.File]::Create($OutputPath))
try {
    # ICO header followed by directory entries and the PNG image payloads.
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = [byte]($frame.Size % 256)
        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Data.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Data.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
} finally {
    $writer.Dispose()
}
