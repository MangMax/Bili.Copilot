param([Parameter(Mandatory)][string]$Assets)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[Drawing.Image]::FromFile((Join-Path $Assets 'Cover.png'))
try {
    $small=[Drawing.Bitmap]::new($source,100,56)
    try {$small.Save((Join-Path $Assets 'Small.png'),[Drawing.Imaging.ImageFormat]::Png)}
    finally {$small.Dispose()}
} finally {$source.Dispose()}
