param([Parameter(Mandatory)][string]$Stage)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$results=@(Get-ChildItem $Stage -Filter 'matrix-avatar-*.png' | ForEach-Object {
    $index=[int]([regex]::Match($_.BaseName,'(\d+)$').Value)
    $bmp=[Drawing.Bitmap]::new($_.FullName)
    try {
        $color=$bmp.GetPixel([int]($bmp.Width/2),[int]($bmp.Height/4))
        $expected=@((40+($index*37%180)),(40+($index*71%180)),(40+($index*101%180)))
        $actual=@([int]$color.R,[int]$color.G,[int]$color.B)
        $delta=0;0..2 | ForEach-Object {$delta=[Math]::Max($delta,[Math]::Abs($actual[$_]-$expected[$_]))}
        @{name='VisibleAvatarPixelsMatchCurrentItem';index=$index;expected=$expected;actual=$actual;alpha=$color.A;pass=($color.A -eq 255 -and $delta -le 3)}
    } finally {$bmp.Dispose()}
})
ConvertTo-Json -InputObject $results -Depth 5 | Set-Content "$Stage/avatar-pixels.json"
$bad=@($results | Where-Object {!$_.pass})
Write-Output "Avatar pixel checks: $($results.Count), failures: $($bad.Count)"
if(!$results.Count -or $bad.Count){exit 2}
