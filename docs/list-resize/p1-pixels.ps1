param([string]$Stage='half-extra',[string]$UserStage='half-user-png',[string]$Output='pixels-half')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../artifacts/list-p1")
$out=Join-Path $root $Output
if(Test-Path $out){throw 'Fresh pixel evidence directory required'}
New-Item -ItemType Directory $out | Out-Null
function Get-Pixels([string]$path){
    $bitmap=[Drawing.Bitmap]::new($path)
    try {
        $rect=[Drawing.Rectangle]::new(0,0,$bitmap.Width,$bitmap.Height)
        $data=$bitmap.LockBits($rect,[Drawing.Imaging.ImageLockMode]::ReadOnly,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $bytes=[byte[]]::new([Math]::Abs($data.Stride)*$data.Height)
            [Runtime.InteropServices.Marshal]::Copy($data.Scan0,$bytes,0,$bytes.Length)
            @{width=$bitmap.Width;height=$bitmap.Height;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))}
        } finally {$bitmap.UnlockBits($data)}
    } finally {$bitmap.Dispose()}
}
$checks=@()
foreach($kind in @('False','True')){
    $first=Get-Pixels "$root/$Stage/matrix-$kind-first.png"
    $same=Get-Pixels "$root/$Stage/matrix-$kind-same.png"
    $other=Get-Pixels "$root/$Stage/matrix-$kind-replacement.png"
    $pass=$first.sha256 -eq $same.sha256 -and $first.sha256 -ne $other.sha256
    $checks+=@{name="DecodedPixels-$kind";pass=$pass;first=$first;same=$same;replacement=$other}
}
foreach($stage in @('baseline','candidate',$UserStage)){
    $bitmap=[Drawing.Bitmap]::new("$root/$stage/matrix-User-50.png")
    try {
        # Exact physical-pixel rectangle, no scaling or interpolation.
        $crop=$bitmap.Clone([Drawing.Rectangle]::new(0,0,110,120),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {$crop.Save("$out/$stage-avatar.png",[Drawing.Imaging.ImageFormat]::Png)} finally {$crop.Dispose()}
    } finally {$bitmap.Dispose()}
}
$checks | ConvertTo-Json -Depth 6 | Set-Content "$out/checks.json"
if(@($checks | Where-Object {-not $_.pass}).Count){throw 'Decoded-pixel comparison failed'}
