param([Parameter(Mandatory)][string]$Stage)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location) -TypeDefinition @'
public static class ContinuityPixelGrid {
 public static byte[] Read(System.Drawing.Bitmap b, int left, int right, int top, int bottom) {
  var data=new byte[((right-left+9)/10)*((bottom-top+9)/10)*3];int k=0;
  for(int y=top;y<bottom;y+=10)for(int x=left;x<right;x+=10){var c=b.GetPixel(x,y);data[k++]=c.R;data[k++]=c.G;data[k++]=c.B;}
  return data;
 }
 public static double[] Fractions(byte[] data) {
  int background=0,colored=0;
  for(int i=0;i<data.Length;i+=3){int r=data[i],g=data[i+1],b=data[i+2];if(r<6&&g<6&&b<6)background++;if(System.Math.Max(r,System.Math.Max(g,b))-System.Math.Min(r,System.Math.Min(g,b))>25)colored++;}
  return new double[]{background/(data.Length/3.0),colored/(data.Length/3.0)};
 }
}
'@
$dir=Join-Path (Resolve-Path $Stage) 'continuous.frames'
$gridDir=Join-Path (Resolve-Path $Stage) 'pixel-grids'
New-Item -ItemType Directory -Path $gridDir -Force | Out-Null
$manifest=Get-Content "$dir/manifest.json" -Raw | ConvertFrom-Json
$frames=@(Get-Content "$dir/frames.ndjson" | ForEach-Object {$_ | ConvertFrom-Json})
$cache=@{}
$results=@(Get-ChildItem $Stage -Filter 'matrix-continuity-*.json' | ForEach-Object {
 $raw=Get-Content $_.FullName -Raw | ConvertFrom-Json
 $start=([datetimeoffset]$raw.startedUtc-[datetimeoffset]$manifest.startedUtc).TotalMilliseconds
 $end=$start+$raw.samples[-1].ms
 foreach($frame in $frames | Where-Object {$_.elapsedMs -ge $start -and $_.elapsedMs -le $end}) {
  if(!$cache.ContainsKey($frame.file)) {
   $b=[Drawing.Bitmap]::new((Join-Path $dir $frame.file))
   try {
    # Fixed 1400x900 window, 150% DPI: viewport bottom and height come from native raw.
    $scale=$b.Width/1382.0;$bottom=$b.Height-20*$scale;$top=$bottom-$raw.samples[0].height*$raw.dpi*$scale
    $left=[int](35*$scale);$right=[int][Math]::Ceiling($b.Width-35*$scale);$firstY=[int]$top+4;$lastY=[int][Math]::Ceiling($bottom)
    $data=[ContinuityPixelGrid]::Read($b,$left,$right,$firstY,$lastY)
    $fractions=[ContinuityPixelGrid]::Fractions($data)
    $gridFile=([IO.Path]::GetFileNameWithoutExtension($frame.file))+'.rgb'
    [IO.File]::WriteAllBytes((Join-Path $gridDir $gridFile),$data)
    $cache[$frame.file]=@{background=$fractions[0];colored=$fractions[1];grid=$gridFile;left=$left;right=$right;top=$firstY;bottom=$lastY;stride=10;jpegSha256=(Get-FileHash (Join-Path $dir $frame.file) -Algorithm SHA256).Hash}
   }finally{$b.Dispose()}
  }
  @{count=$raw.count;ms=$frame.elapsedMs-$start;file=$frame.file;background=$cache[$frame.file].background;colored=$cache[$frame.file].colored;raw=$cache[$frame.file]}
 }
})
$results | ConvertTo-Json -Depth 4 | Set-Content "$Stage/video-pixels.json"
$results | Group-Object count | ForEach-Object { "$($_.Name): samples=$($_.Count), >90% background=$(@($_.Group | Where-Object background -gt .9).Count), >50% background=$(@($_.Group | Where-Object background -gt .5).Count), max background=$(($_.Group.background | Measure-Object -Maximum).Maximum)" }
