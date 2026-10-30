# Fixed package/date, read-only. Never output raw messages, URLs or account fields.
[CmdletBinding()]
param(
 [ValidatePattern('\A(?:[01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]\.[0-9]{3}\z')][string]$StartTime='14:33:00.000',
 [Parameter(Mandatory)][ValidatePattern('\A(?:[01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]\.[0-9]{3}\z')][string]$EndTime
)
$ErrorActionPreference='Stop'
if($StartTime -ge $EndTime){throw 'Expected nonempty same-day time window'}
$family='Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg'
$pkg=Get-AppxPackage -Name 'Mang.BiliCopilot.PerfAcceptance.Caa6a7be'
if(@($pkg).Count -ne 1 -or $pkg.PackageFamilyName -ne $family){throw 'Package mismatch'}
$path=Join-Path $env:LOCALAPPDATA "Packages/$family/LocalState/Logger/log-2026-10-30.txt"
$stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
$reader=[IO.StreamReader]::new($stream)
$categories=[ordered]@{recommend='尝试加载推荐视频时出错';hot='尝试加载热门视频时出错';rank='尝试加载全站排行榜视频时出错'}
try {
 $raw=$reader.ReadToEnd()
 $blocks=[regex]::Split($raw,'(?m)(?=^2026-10-30 \d{2}:\d{2}:\d{2}\.\d{3})')
 $rows=@(foreach($block in $blocks){
  if($block -notmatch '^2026-10-30 (\d{2}:\d{2}:\d{2}\.\d{3})'){continue}
  $time=$Matches[1]
  if($time -lt $StartTime -or $time -ge $EndTime){continue}
  $header=($block -split '\r?\n',2)[0]
  foreach($key in $categories.Keys){if($header.Contains($categories[$key])){
   [ordered]@{time=$time;category=$key;blockTextSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($block)))}
  }}
 })
 [ordered]@{date='2026-10-30';timezone='UTC+08';startInclusive=$StartTime;endExclusive=$EndTime;count=$rows.Count;errors=$rows;meaning='Only three fixed application error messages; not HTTP success, rate-limit absence, or complete error coverage'}|ConvertTo-Json -Depth 5
} finally {$reader.Dispose();$stream.Dispose()}
