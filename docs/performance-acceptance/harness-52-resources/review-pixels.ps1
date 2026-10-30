param([Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
function Pixels($bitmap,[int]$left,[int]$top,[int]$width,[int]$height) {
 $bytes=[byte[]]::new($width*$height*3); $offset=0; $nonWhite=0
 for($y=$top;$y -lt $top+$height;$y++) { for($x=$left;$x -lt $left+$width;$x++) {
  $p=$bitmap.GetPixel($x,$y);$bytes[$offset++]=$p.R;$bytes[$offset++]=$p.G;$bytes[$offset++]=$p.B
  if($p.R -ne 255 -or $p.G -ne 255 -or $p.B -ne 255){$nonWhite++}
 } }
 @{sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes));nonWhitePixels=$nonWhite;bounds=@($left,$top,($left+$width),($top+$height))}
}
$svg=[Drawing.Bitmap]::FromFile((Resolve-Path "$Evidence/svg-comparison.png"))
$result=@{svg=@{};portraits=@{};originals=@()}
try {
 if($svg.Width -ne 1086 -or $svg.Height -ne 893){throw 'Reinspect original crop coordinates for this capture size'}
 foreach($kind in @('path','text')) {
  $top=if($kind -eq 'path'){142}else{283}
  $crops=@(18,280,542 | ForEach-Object {Pixels $svg $_ $top 240 100})
  $result.svg[$kind]=@{crops=$crops;pixelIdentical=(@($crops.sha256 | Select-Object -Unique).Count -eq 1)}
 }
} finally {$svg.Dispose()}
foreach($name in @('portrait-complete','dpi-left','dpi-right')) {
 $bitmap=[Drawing.Bitmap]::FromFile((Resolve-Path "$Evidence/$name.png"))
 try {$result.portraits[$name]=Pixels $bitmap 17 145 240 480} finally {$bitmap.Dispose()}
}
$result.portraitsIdentical=(@($result.portraits.Values.sha256 | Select-Object -Unique).Count -eq 1)
$result.originals=@(Get-ChildItem "$Evidence/*.png" | Get-FileHash | Select-Object Path,Hash)
$result | ConvertTo-Json -Depth 8 | Set-Content "$Evidence/pixel-review.json"
