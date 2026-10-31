param([Parameter(Mandatory)][string]$Stage)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$raw=Get-Content "$Stage/matrix-related.json" -Raw | ConvertFrom-Json
$expectedIndices=@($raw[0].samples[-1].cards | ForEach-Object {$_.index})
$results=@(Get-ChildItem $Stage -Filter 'matrix-season-image-*.png' | ForEach-Object {
    $index=[int]([regex]::Match($_.BaseName,'(\d+)$').Value)
    $bitmap=[Drawing.Bitmap]::new($_.FullName)
    try {
        $color=$bitmap.GetPixel([int]($bitmap.Width/2),[int]($bitmap.Height/4))
        $expected=@((40+3*$index),(80+2*$index),(160-2*$index))
        $actual=@([int]$color.R,[int]$color.G,[int]$color.B)
        $delta=0
        0..2 | ForEach-Object {$delta=[Math]::Max($delta,[Math]::Abs($actual[$_]-$expected[$_]))}
        @{index=$index;actual=$actual;expected=$expected;alpha=$color.A;pass=($color.A -eq 255 -and $delta -le 3)}
    } finally {$bitmap.Dispose()}
})
$actualIndices=@($results | ForEach-Object {$_.index} | Sort-Object)
$expectedIndices=@($expectedIndices | Sort-Object)
$complete=($actualIndices.Count -gt 0 -and (($actualIndices -join ',') -eq ($expectedIndices -join ',')))
@{name='AllSettledVisibleSeasonImagesMatchCurrentItem';complete=$complete;results=$results} | ConvertTo-Json -Depth 6 | Set-Content "$Stage/season-pixels.json"
Write-Output "Season pixels: $($results.Count), complete=$complete, bad=$(@($results | Where-Object {!$_.pass}).Count)"
if(!$complete -or @($results | Where-Object {!$_.pass}).Count){exit 2}
