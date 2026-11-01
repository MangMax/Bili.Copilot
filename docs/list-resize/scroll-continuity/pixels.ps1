param([Parameter(Mandatory)][string]$Stage)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$results=@(Get-ChildItem $Stage -Filter 'matrix-continuity-*.json' | ForEach-Object {
 $raw=Get-Content $_.FullName -Raw | ConvertFrom-Json
 foreach($frame in $raw.pixels){
  $b=[Drawing.Bitmap]::new((Join-Path (Resolve-Path $Stage) $frame.file))
  try {
   $colors=@{}; $total=0; $colored=0
   for($y=4;$y -lt $b.Height;$y+=8){for($x=4;$x -lt $b.Width-20;$x+=8){
    $c=$b.GetPixel($x,$y);$total++;$key=$c.ToArgb().ToString();$colors[$key]++
    if(([Math]::Max($c.R,[Math]::Max($c.G,$c.B))-[Math]::Min($c.R,[Math]::Min($c.G,$c.B))) -gt 25 -and $c.A -gt 200){$colored++}
   }}
   $dominant=($colors.Values | Measure-Object -Maximum).Maximum
   @{count=$raw.count;file=$frame.file;before=$frame.before;after=$frame.after;offset=$frame.offset;offsetAfter=$frame.offsetAfter;dominantFraction=$dominant/$total;coloredFraction=$colored/$total}
  }finally{$b.Dispose()}
 }
})
$results | ConvertTo-Json -Depth 4 | Set-Content "$Stage/pixel-summary.json"
$results | Group-Object count | ForEach-Object { "$($_.Name): frames=$($_.Count), minimum colored fraction=$(($_.Group.coloredFraction | Measure-Object -Minimum).Minimum), maximum single-color fraction=$(($_.Group.dominantFraction | Measure-Object -Maximum).Maximum)" }
