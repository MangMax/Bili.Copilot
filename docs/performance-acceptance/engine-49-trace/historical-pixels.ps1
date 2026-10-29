$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$results = foreach ($n in 1, 2) {
    $path = "docs/performance-acceptance/engine-49-candidate/evidence/final-visual/Win2D-frozen-$n.png"
    $original = "C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-c9d5219a/$path"
    $bitmap = [Drawing.Bitmap]::new((Resolve-Path $path).Path)
    try {
        $regions = foreach ($roi in @(@{name='whiteText';x=890;y=390;w=420;h=55}, @{name='render';x=20;y=320;w=1340;h=480})) {
            $bytes = [Collections.Generic.List[byte]]::new()
            $rgba = [Collections.Generic.List[byte]]::new()
            $white = 0
            $minAlpha = 255
            $maxAlpha = 0
            for ($y=$roi.y; $y -lt ($roi.y+$roi.h); $y++) {
                for ($x=$roi.x; $x -lt ($roi.x+$roi.w); $x++) {
                    $c=$bitmap.GetPixel($x,$y)
                    $bytes.Add($c.R); $bytes.Add($c.G); $bytes.Add($c.B)
                    $rgba.Add($c.R); $rgba.Add($c.G); $rgba.Add($c.B); $rgba.Add($c.A)
                    $minAlpha=[Math]::Min($minAlpha,$c.A); $maxAlpha=[Math]::Max($maxAlpha,$c.A)
                    if ($c.R -gt 150 -and $c.G -gt 150 -and $c.B -gt 150) { $white++ }
                }
            }
            @{roi=$roi;whitePixels=$white;minAlpha=$minAlpha;maxAlpha=$maxAlpha;rgbaSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($rgba.ToArray()));rgbSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes.ToArray()))}
        }
        @{file=$path;fileHash=(Get-FileHash $path).Hash;originalFileHash=(Get-FileHash $original).Hash;regions=$regions}
    } finally { $bitmap.Dispose() }
}
$results | ConvertTo-Json -Depth 6
