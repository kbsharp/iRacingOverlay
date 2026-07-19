# Generates src/IRacingOverlay.App/Assets/app.ico - the application icon used by
# the installer, Add/Remove Programs, the Start menu, the taskbar and the tray.
#
# The icon is committed to the repo; this script exists so it can be regenerated
# (or restyled) rather than being an opaque binary nobody can edit. Run it after
# changing the palette here:
#
#   powershell -ExecutionPolicy Bypass -File tools/MakeAppIcon.ps1
#
# The mark is a rounded dark panel (the widgets' own material) carrying an accent
# radar ring with a warm centre dot - the accent/warm pairing the overlay uses for
# "everyone else" vs "this is you". Drawn at each size rather than scaled from one
# bitmap, so the 16px entry gets its own proportions instead of a smudge.

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "..\src\IRacingOverlay.App\Assets"
$outFile = Join-Path $outDir "app.ico"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

# App palette (App.xaml): panel base, panel top-light, edge, Accent, Warning.
$panelDark = [System.Drawing.ColorTranslator]::FromHtml("#0E141F")
$panelLight = [System.Drawing.ColorTranslator]::FromHtml("#1E2A3B")
$edge = [System.Drawing.ColorTranslator]::FromHtml("#38465C")
$accent = [System.Drawing.ColorTranslator]::FromHtml("#39A7FF")
$warm = [System.Drawing.ColorTranslator]::FromHtml("#FFB03D")

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [float]$size
    $inset = $s * 0.035
    $side = $s - ($inset * 2)
    # Softer corners at small sizes: a 256-radius curve reads as a blob at 16px.
    $radius = if ($size -le 24) { $s * 0.16 } else { $s * 0.21 }

    $panel = New-RoundedPath $inset $inset $side $side $radius
    $rect = New-Object System.Drawing.RectangleF($inset, $inset, $side, $side)
    $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect, $panelLight, $panelDark, [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($fill, $panel)

    # Edge light - skipped below 32px, where a hairline just muddies the shape.
    if ($size -ge 32) {
        $pen = New-Object System.Drawing.Pen($edge, [float]($s * 0.016))
        $g.DrawPath($pen, $panel)
        $pen.Dispose()
    }

    # Radar ring, open at the bottom right so it reads as a sweep, not an "O".
    $ringPen = New-Object System.Drawing.Pen($accent, [float]($s * 0.085))
    $ringPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ringPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $m = $s * 0.28
    $g.DrawArc($ringPen, $m, $m, $s - ($m * 2), $s - ($m * 2), 145, 250)
    $ringPen.Dispose()

    # Centre dot: "this is you", in the same warm the player row uses.
    $dot = $s * 0.16
    $dotBrush = New-Object System.Drawing.SolidBrush($warm)
    $g.FillEllipse($dotBrush, ($s - $dot) / 2, ($s - $dot) / 2, $dot, $dot)
    $dotBrush.Dispose()

    $fill.Dispose()
    $panel.Dispose()
    $g.Dispose()
    return $bmp
}

# Encodes one image as a classic DIB icon entry: a BITMAPINFOHEADER whose height
# is doubled (colour rows + mask rows), 32bpp BGRA bottom-up, then a 1bpp AND
# mask. Fully opaque, since the alpha channel carries the real shape - but the
# mask must still be present and row-padded to 4 bytes or the entry is rejected.
#
# Why not PNG for every entry: System.Drawing.Icon cannot decode PNG-compressed
# entries, and the tray icon goes through exactly that path (NotifyIcon). Explorer
# copes, so the two largest sizes - where DIB gets expensive - stay PNG.
function ConvertTo-DibEntry([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $data = $bmp.LockBits(
        (New-Object System.Drawing.Rectangle(0, 0, $w, $h)),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($data.Stride * $h)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([UInt32]40)            # BITMAPINFOHEADER size
    $bw.Write([Int32]$w)
    $bw.Write([Int32]($h * 2))       # colour rows + mask rows
    $bw.Write([UInt16]1)             # planes
    $bw.Write([UInt16]32)            # bpp
    $bw.Write([UInt32]0)             # BI_RGB
    $bw.Write([UInt32]($w * $h * 4))
    $bw.Write([Int32]0); $bw.Write([Int32]0); $bw.Write([UInt32]0); $bw.Write([UInt32]0)

    # Bottom-up colour rows.
    for ($y = $h - 1; $y -ge 0; $y--) {
        $bw.Write($pixels, $y * $data.Stride, $w * 4)
    }
    # AND mask: all-zero (opaque), padded to a 4-byte row stride.
    $maskStride = [Math]::Floor((($w + 31) / 32)) * 4
    $bw.Write((New-Object byte[] ($maskStride * $h)))

    $bw.Flush()
    $bytes = $ms.ToArray()
    $bw.Dispose(); $ms.Dispose()
    # Leading comma: PowerShell unrolls a returned array into the pipeline, which
    # turns this byte[] into an Object[] of boxed bytes at the call site - and
    # BinaryWriter.Write then binds to the single-byte overload and writes one
    # byte per entry, producing a directory that promises data the file doesn't
    # contain. Icons that Explorer renders but System.Drawing rejects.
    return , $bytes
}

# DIB for the sizes the tray/window code loads via System.Drawing.Icon; PNG for
# the two largest, where a DIB would add ~320KB for no visible gain.
$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$pngSizes = @(128, 256)
$images = @()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size
    if ($pngSizes -contains $size) {
        $stream = New-Object System.IO.MemoryStream
        $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $stream.ToArray()
        $stream.Dispose()
    }
    else {
        $bytes = ConvertTo-DibEntry $bmp
    }
    $images += , @{ Size = $size; Bytes = $bytes }
    $bmp.Dispose()
}

$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($out)
$writer.Write([UInt16]0)            # reserved
$writer.Write([UInt16]1)            # type: icon
$writer.Write([UInt16]$images.Count)

# Directory entries come first, so every image's offset must account for the
# whole directory: 6-byte header + 16 bytes per entry.
$offset = 6 + (16 * $images.Count)
foreach ($image in $images) {
    # 256 is stored as 0 - the width/height fields are a single byte each.
    $dim = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([Byte]$dim)
    $writer.Write([Byte]$dim)
    $writer.Write([Byte]0)          # palette count (0 = truecolour)
    $writer.Write([Byte]0)          # reserved
    $writer.Write([UInt16]1)        # colour planes
    $writer.Write([UInt16]32)       # bits per pixel
    $writer.Write([UInt32]$image.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image.Bytes) }

$writer.Flush()
[System.IO.File]::WriteAllBytes($outFile, $out.ToArray())
$writer.Dispose()
$out.Dispose()

Write-Output "Wrote $outFile ($((Get-Item $outFile).Length) bytes, sizes: $($sizes -join ', '))"
