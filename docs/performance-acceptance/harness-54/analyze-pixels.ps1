param([Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
# Use Windows PowerShell's installed System.Drawing, without installing Python packages.
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System.Drawing;
public static class Pixel54 {
 public static long[] Count(string path) {
  using(var b=new Bitmap(path)) {
   long white=0,dark=0;
   for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++) {
    var c=b.GetPixel(x,y);
    if(c.R==255&&c.G==255&&c.B==255)white++;
    if(c.R==32&&c.G==32&&c.B==32)dark++;
   }
   return new long[]{b.Width,b.Height,white,dark};
  }
 }
}
'@
$base=(Resolve-Path $Evidence).Path
$pixels=@(Get-ChildItem $base -Recurse -Filter '*.png' | ForEach-Object {
    $c=[Pixel54]::Count($_.FullName)
    [pscustomobject]@{file=$_.FullName.Substring($base.Length+1);width=$c[0];height=$c[1];whitePixels=$c[2];dark202020Pixels=$c[3];whiteFraction=$c[2]/($c[0]*$c[1]);darkFraction=$c[3]/($c[0]*$c[1]);sha256=(Get-FileHash $_.FullName).Hash}
})
if($pixels.Count -ne 17){throw 'Expected all 17 recheck screenshots'}
$pixels|ConvertTo-Json|Set-Content "$base/pixel-analysis.json"
$pixels|Select-Object file,whiteFraction,darkFraction
