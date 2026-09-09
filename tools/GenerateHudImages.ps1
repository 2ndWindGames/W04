Add-Type -AssemblyName System.Drawing
$outputDirectory = Join-Path $PSScriptRoot '../Assets/Resources/UI'
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
foreach ($name in @('sound-on', 'sound-off', 'share', 'circle', 'pill')) {
    $width = if ($name -eq 'pill') { 432 } else { 192 }
    $height = if ($name -eq 'pill') { 144 } else { 192 }
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 11)
    $pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    if ($name -eq 'circle') {
        $graphics.FillEllipse([System.Drawing.Brushes]::White, 2, 2, 188, 188)
    } elseif ($name -eq 'pill') {
        $path.AddArc(2, 2, 140, 140, 90, 180)
        $path.AddArc(290, 2, 140, 140, 270, 180)
        $path.CloseFigure()
        $graphics.FillPath([System.Drawing.Brushes]::White, $path)
    } elseif ($name -eq 'share') {
        $graphics.DrawLine($pen, 96, 113, 96, 37)
        $graphics.DrawLines($pen, [System.Drawing.Point[]]@([System.Drawing.Point]::new(65, 68), [System.Drawing.Point]::new(96, 37), [System.Drawing.Point]::new(127, 68)))
        $graphics.DrawLines($pen, [System.Drawing.Point[]]@([System.Drawing.Point]::new(48, 100), [System.Drawing.Point]::new(48, 149), [System.Drawing.Point]::new(144, 149), [System.Drawing.Point]::new(144, 100)))
    } else {
        $path.AddPolygon([System.Drawing.Point[]]@([System.Drawing.Point]::new(36, 77), [System.Drawing.Point]::new(63, 77), [System.Drawing.Point]::new(94, 49), [System.Drawing.Point]::new(94, 143), [System.Drawing.Point]::new(63, 115), [System.Drawing.Point]::new(36, 115)))
        $graphics.DrawPath($pen, $path)
        if ($name -eq 'sound-on') {
            $graphics.DrawArc($pen, 90, 66, 48, 60, -55, 110)
            $graphics.DrawArc($pen, 89, 43, 83, 106, -55, 110)
        } else {
            $graphics.DrawLine($pen, 124, 80, 153, 112)
            $graphics.DrawLine($pen, 153, 80, 124, 112)
        }
    }
    $bitmap.Save((Join-Path $outputDirectory "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $path.Dispose()
    $pen.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}
