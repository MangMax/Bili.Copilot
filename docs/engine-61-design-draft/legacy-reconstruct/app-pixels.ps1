param([Parameter(Mandatory)][string]$InputDirectory,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$inputRoot=(Resolve-Path $InputDirectory).Path
$out=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -ErrorAction Stop | Out-Null
# Fixed 1680x1110 fixture window. This region contains only the TOP glyphs.
# Compare decoded pixels, not a model's possibly compressed rendering of a repeated image.
$rect=[Drawing.Rectangle]::new(710,18,260,40)
function Read-Glyph([string]$path) {
    $bitmap=[Drawing.Bitmap]::new($path)
    try {
        if($bitmap.Width -ne 1680 -or $bitmap.Height -ne 1110){throw "Unexpected capture geometry: $path"}
        $bytes=[Collections.Generic.List[byte]]::new()
        for($y=$rect.Top;$y -lt $rect.Bottom;$y++) {
            for($x=$rect.Left;$x -lt $rect.Right;$x++) {
                $c=$bitmap.GetPixel($x,$y)
                $bytes.Add($c.R);$bytes.Add($c.G);$bytes.Add($c.B)
            }
        }
        $crop=$bitmap.Clone($rect,$bitmap.PixelFormat)
        try {$crop.Save((Join-Path $out ([IO.Path]::GetFileNameWithoutExtension($path)+'-glyph.png')))} finally {$crop.Dispose()}
        $raw=$bytes.ToArray()
        return @{sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($raw));nonzero=@($raw|Where-Object {$_ -gt 20}).Count}
    } finally {$bitmap.Dispose()}
}
$reference=Read-Glyph (Join-Path $inputRoot 'screen-remount1-stable.png')
if($reference.nonzero -lt 1000){throw 'Reference is empty; requires human PRIOR19 review'}
$blankHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]::new(260*40*3)))
if($blankHash -eq $reference.sha256){throw 'Blank negative control was accepted'}
$records=@(foreach($p in Get-ChildItem $inputRoot -Filter '*.png' | Where-Object { $_.Name -match '^(screen-|static-)' } | Sort-Object Name) {
    $g=Read-Glyph $p.FullName
    [pscustomobject]@{file=$p.Name;fileSha256=(Get-FileHash $p.FullName).Hash;glyphSha256=$g.sha256;nonzero=$g.nonzero;matchesPrior19=$g.sha256 -eq $reference.sha256}
})
@{rectangle=@(710,18,260,40);reference='screen-remount1-stable.png';blankNegativeRejected=$true;records=$records}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $out 'pixels.json')
$records | Select-Object file,nonzero,matchesPrior19 | Format-Table

