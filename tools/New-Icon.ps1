param([string]$Output = (Join-Path $PSScriptRoot '../images/icon.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$outputPath = [IO.Path]::GetFullPath($Output)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputPath)) | Out-Null
$bitmap = [Drawing.Bitmap]::new(512, 512)
$g = [Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
$g.Clear([Drawing.Color]::Transparent)
function Color([string]$hex) { [Drawing.ColorTranslator]::FromHtml($hex) }
function Polygon([string]$hex, [float[]]$xy) {
    $points = [Drawing.PointF[]]::new($xy.Length / 2)
    for ($i=0; $i -lt $points.Length; $i++) { $points[$i] = [Drawing.PointF]::new($xy[$i*2],$xy[$i*2+1]) }
    $brush = [Drawing.SolidBrush]::new((Color $hex))
    try { $g.FillPolygon($brush,$points) } finally { $brush.Dispose() }
}
$frame = [Drawing.Drawing2D.GraphicsPath]::new()
$frame.AddArc(12,12,64,64,180,90); $frame.AddArc(436,12,64,64,270,90)
$frame.AddArc(436,436,64,64,0,90); $frame.AddArc(12,436,64,64,90,90); $frame.CloseFigure()
$background = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(0,0),[Drawing.Point]::new(512,512),(Color '#22392F'),(Color '#090F11'))
$rim = [Drawing.Pen]::new((Color '#B99C5D'),7)
try {
    $g.FillPath($background,$frame); $g.DrawPath($rim,$frame)
    $gold = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(70,55),[Drawing.Point]::new(430,460),(Color '#F2DDA5'),(Color '#6A4D28'))
    $g.FillEllipse($gold,54,46,404,404); $gold.Dispose()
    $face = [Drawing.SolidBrush]::new((Color '#101C1C')); $g.FillEllipse($face,71,63,370,370); $face.Dispose()
    $fine = [Drawing.Pen]::new((Color '#AB935A'),2); $g.DrawEllipse($fine,82,74,348,348); $fine.Dispose()
    for ($i=0; $i -lt 64; $i++) {
        $angle=$i*[Math]::PI/32; $inner=174; if ($i%8 -eq 0) { $inner=155 }
        $tick = [Drawing.Pen]::new((Color '#C6AE75'), $(if($i%8 -eq 0){4}else{2}))
        $g.DrawLine($tick,[float](256+[Math]::Cos($angle)*$inner),[float](248+[Math]::Sin($angle)*$inner),[float](256+[Math]::Cos($angle)*180),[float](248+[Math]::Sin($angle)*180))
        $tick.Dispose()
    }
    # A readable trail and endpoint, matching the in-game route color.
    $route = [Drawing.Pen]::new((Color '#52DEB0'),9)
    $route.StartCap=[Drawing.Drawing2D.LineCap]::Round; $route.EndCap=[Drawing.Drawing2D.LineCap]::Round
    $trail = [Drawing.PointF[]]@([Drawing.PointF]::new(115,359),[Drawing.PointF]::new(153,321),[Drawing.PointF]::new(188,339),[Drawing.PointF]::new(224,301))
    $g.DrawLines($route,$trail); $route.Dispose()
    $point = [Drawing.SolidBrush]::new((Color '#B3FFE0')); $g.FillEllipse($point,104,348,22,22); $point.Dispose()
    Polygon '#030A0A' @(365,99,206,309,260,279,295,332)
    Polygon '#98713A' @(357,91,198,301,252,271)
    Polygon '#F7E9BD' @(357,91,252,271,287,324)
    $edge=[Drawing.Pen]::new((Color '#FFF2CF'),3)
    $g.DrawLine($edge,357,91,198,301); $g.DrawLine($edge,357,91,287,324); $edge.Dispose()
    $hub=[Drawing.SolidBrush]::new((Color '#DCC58B')); $g.FillEllipse($hub,244,240,24,24); $hub.Dispose()
    Polygon '#E6D198' @(256,450,267,467,256,484,245,467)
    $bitmap.Save($outputPath,[Drawing.Imaging.ImageFormat]::Png)
} finally { $background.Dispose(); $rim.Dispose(); $frame.Dispose(); $g.Dispose(); $bitmap.Dispose() }
Write-Output $outputPath
