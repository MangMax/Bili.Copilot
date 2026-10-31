param([string]$InputDirectory,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=(Resolve-Path $InputDirectory).Path
$out=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory $out -ErrorAction Stop | Out-Null
$records=@(foreach($file in Get-ChildItem $root -Filter '*.png') {
 $bitmap=[Drawing.Bitmap]::new($file.FullName)
 try {
  if($bitmap.Width -ne 1282 -or $bitmap.Height -ne 891){throw 'Unexpected native fixture geometry'}
  $rect=[Drawing.Rectangle]::new(450,290,380,65)
  if($file.BaseName -eq 'BOTTOM'){$rect=[Drawing.Rectangle]::new(450,765,380,65)}
  $bytes=[Collections.Generic.List[byte]]::new()
  for($y=$rect.Top;$y -lt $rect.Bottom;$y++){for($x=$rect.Left;$x -lt $rect.Right;$x++){$c=$bitmap.GetPixel($x,$y);$bytes.Add($c.R);$bytes.Add($c.G);$bytes.Add($c.B)}}
  $crop=$bitmap.Clone($rect,$bitmap.PixelFormat)
  try{$crop.Save((Join-Path $out $file.Name))}finally{$crop.Dispose()}
  $raw=$bytes.ToArray()
  @{file=$file.Name;rect=@($rect.X,$rect.Y,$rect.Width,$rect.Height);sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($raw));nonzero=@($raw|Where-Object{$_ -gt 20}).Count}
 }finally{$bitmap.Dispose()}
})
$records|ConvertTo-Json -Depth 6|Set-Content (Join-Path $out 'pixels.json')
