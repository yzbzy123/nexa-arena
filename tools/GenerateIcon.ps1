$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class NativeIconCleanup {
    [DllImport("user32.dll", SetLastError=true)] public static extern bool DestroyIcon(IntPtr hIcon);
}
'@
$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$path=Join-Path $root 'assets\NexaArena.ico'
New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
$bitmap=[System.Drawing.Bitmap]::new(256,256,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g=[System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$rect=[System.Drawing.Rectangle]::new(0,0,256,256)
$brush=[System.Drawing.Drawing2D.LinearGradientBrush]::new($rect,[System.Drawing.Color]::FromArgb(37,99,235),[System.Drawing.Color]::FromArgb(14,165,233),45)
$g.FillRectangle($brush,$rect)
$brush.Dispose()
$pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(90,255,255,255),10)
$g.DrawRectangle($pen,12,12,232,232)
$pen.Dispose()
$font=[System.Drawing.Font]::new('Arial',132,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$format=[System.Drawing.StringFormat]::new(); $format.Alignment='Center'; $format.LineAlignment='Center'
$textBrush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$g.DrawString('N',$font,$textBrush,[System.Drawing.RectangleF]::new(0,-4,256,256),$format)
$textBrush.Dispose(); $font.Dispose(); $format.Dispose(); $g.Dispose()
$handle=$bitmap.GetHicon(); $icon=[System.Drawing.Icon]::FromHandle($handle)
$stream=[System.IO.File]::Create($path); $icon.Save($stream); $stream.Dispose(); $icon.Dispose()
[NativeIconCleanup]::DestroyIcon($handle) | Out-Null
$bitmap.Dispose()
Write-Host "Generated $path"
