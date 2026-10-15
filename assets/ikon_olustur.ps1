# assets\ikon.ico dosyasını üretir (16-256 px, PNG sıkıştırmalı). Bir kez çalıştırmak yeterli.
Add-Type -AssemblyName System.Drawing

function Ciz([int]$s) {
    $bmp = New-Object Drawing.Bitmap $s, $s
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)
    $r = [float]($s * 0.22)
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $w = [float]($s - 1)
    $path.AddArc(0, 0, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($w - 2 * $r, 0, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($w - 2 * $r, $w - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc(0, $w - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $grad = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Point 0, 0), (New-Object Drawing.Point $s, $s), [Drawing.Color]::FromArgb(58, 91, 217), [Drawing.Color]::FromArgb(31, 170, 120))
    $g.FillPath($grad, $path)
    # nabız çizgisi
    $kalem = New-Object Drawing.Pen ([Drawing.Color]::White), ([float][math]::Max(1.5, $s * 0.085))
    $kalem.LineJoin = 'Round'; $kalem.StartCap = 'Round'; $kalem.EndCap = 'Round'
    $p = @(@(0.14, 0.55), @(0.34, 0.55), @(0.44, 0.30), @(0.56, 0.76), @(0.66, 0.48), @(0.86, 0.48)) | ForEach-Object { New-Object Drawing.PointF ([float]($_[0] * $s)), ([float]($_[1] * $s)) }
    $g.DrawLines($kalem, [Drawing.PointF[]]$p)
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$boyutlar = 16, 24, 32, 48, 64, 128, 256
$resimler = foreach ($b in $boyutlar) { , (Ciz $b) }
$out = New-Object IO.MemoryStream
$bw = New-Object IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$boyutlar.Count)
$ofset = 6 + 16 * $boyutlar.Count
for ($i = 0; $i -lt $boyutlar.Count; $i++) {
    $b = $boyutlar[$i]; $veri = $resimler[$i]
    $bw.Write([byte]($b % 256)); $bw.Write([byte]($b % 256)); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$veri.Length); $bw.Write([uint32]$ofset)
    $ofset += $veri.Length
}
foreach ($veri in $resimler) { $bw.Write($veri) }
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'ikon.ico'), $out.ToArray())
Write-Host "ikon.ico oluşturuldu"
