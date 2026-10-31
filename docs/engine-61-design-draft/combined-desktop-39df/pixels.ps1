param([string]$Run='runtime2')
Add-Type -AssemblyName System.Drawing
$base=Join-Path (Get-Location) "artifacts/combined-60cd/full/$Run"
$out=Join-Path $base 'pixels';New-Item -ItemType Directory -Force $out|Out-Null
$result=@()
foreach($engine in @('core','legacy')) {
 foreach($suffix in @('positive','blank','other')) {
  $file=Join-Path $base "$engine-$suffix.png";$bmp=[Drawing.Bitmap]::FromFile($file)
  $regions=@(@('reverse',220,5,220,60),@('top',720,5,240,60),@('rolling',1260,5,200,60),@('bottom',710,1050,250,38),@('future',720,50,240,50))
  foreach($r in $regions) {
   $crop=$bmp.Clone([Drawing.Rectangle]::new($r[1],$r[2],$r[3],$r[4]),$bmp.PixelFormat)
   $path=Join-Path $out "$engine-$suffix-$($r[0]).png";$crop.Save($path)
   $bright=0
   for($y=0;$y -lt $crop.Height;$y++){for($x=0;$x -lt $crop.Width;$x++){
    $c=$crop.GetPixel($x,$y);if($c.R -gt 100 -and [Math]::Abs([int]$c.R-$c.G) -lt 10 -and [Math]::Abs([int]$c.R-$c.B) -lt 10){$bright++}
   }}
   $result+=@{engine=$engine;frame=$suffix;region=$r[0];source=$file;box=@($r[1],$r[2],$r[3],$r[4]);unscaled=$true;brightNeutralPixels=$bright;sha256=(Get-FileHash $path).Hash}
   $crop.Dispose()
  }
  $bmp.Dispose()
 }
}
$result | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $out 'pixel-measurements.json')
foreach($engine in @('core','legacy')){foreach($region in @('reverse','top','rolling','bottom')){
 $pos=$result|Where-Object {$_.engine -eq $engine -and $_.region -eq $region -and $_.frame -eq 'positive'}
 $neg=$result|Where-Object {$_.engine -eq $engine -and $_.region -eq $region -and $_.frame -eq 'blank'}
 if($pos.brightNeutralPixels -lt 100 -or $neg.brightNeutralPixels -ge 40){throw "Foreground/blank mismatch $engine $region"}
}}
