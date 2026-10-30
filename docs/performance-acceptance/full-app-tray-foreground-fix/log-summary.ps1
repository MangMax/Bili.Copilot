param([Parameter(Mandatory)][string]$Run)
$ErrorActionPreference='Stop'
$folder=Join-Path $PSScriptRoot $Run
$r=Get-Content "$folder/exit-observation.json" -Raw|ConvertFrom-Json -DateKind String
$start=[DateTimeOffset]::Parse($r.startUtc).ToLocalTime()
$end=[DateTimeOffset]::Parse($r.collectionEndUtc).ToLocalTime()
if($start.Date -ne $end.Date){throw 'Same-day log scan only'}
$family='Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg'
if((Get-AppxPackage -Name Mang.BiliCopilot.PerfAcceptance.Caa6a7be).PackageFamilyName -ne $family){throw 'Wrong package'}
$date=$start.ToString('yyyy-MM-dd')
$file=Join-Path $env:LOCALAPPDATA "Packages/$family/LocalState/Logger/log-$date.txt"
$stream=[IO.File]::Open($file,'Open','Read','ReadWrite');$reader=[IO.StreamReader]::new($stream)
try{
 $raw=$reader.ReadToEnd();$blocks=[regex]::Split($raw,'(?m)(?=^'+$date+' \d{2}:\d{2}:\d{2}\.\d{3})')
 $rows=@(foreach($b in $blocks){
  if($b -notmatch ('^'+$date+' (\d{2}:\d{2}:\d{2}\.\d{3})')){continue}
  $time=$Matches[1];if($time -lt $start.ToString('HH:mm:ss.fff') -or $time -gt $end.ToString('HH:mm:ss.fff')){continue}
  $header=($b -split '\r?\n',2)[0]
  $category=if($header.Contains('Unhandled exception occurred.')){'managed-unhandled'}elseif($header.Contains('获取弹幕分片失败')){'danmaku-segment'}elseif($header.Contains('搜索建议')){'search-suggestion'}elseif($header.Contains('退出应用时失败')){'exit-failure'}elseif($header.Contains('无法获取登录用户的资料')){'profile-initialization-failure'}elseif($b -match 'Exception'){'other-exception'}else{continue}
  $allowed=@('System.Threading.Tasks.TaskCanceledException','System.OperationCanceledException','System.IO.IOException','System.Net.Sockets.SocketException','System.Runtime.InteropServices.COMException','System.NullReferenceException','RichasyKernel.KernelException','System.Exception')
  $types=@($allowed|Where-Object {$b.Contains($_)})
  [pscustomobject]@{time=$time;category=$category;types=$types}
 })
 $unhandled=@($rows|Where-Object category -eq managed-unhandled).Count
 @{windowStart=$start.ToString('o');windowEnd=$end.ToString('o');groups=@($rows|Group-Object category|ForEach-Object {@{category=$_.Name;count=$_.Count;first=$_.Group[0].time;last=$_.Group[-1].time;allowedTypes=@($_.Group.types|Select-Object -Unique)}});managedUnhandled=$unhandled;noRawMessageOrUrlSaved=$true}|ConvertTo-Json -Depth 6|Set-Content "$folder/log-summary.json"
 $r.managedUnhandled=$unhandled;$r | Add-Member -NotePropertyName managedLogScan -NotePropertyValue 'complete' -Force;$r|ConvertTo-Json -Depth 8|Set-Content "$folder/exit-observation.json"
}finally{$reader.Dispose();$stream.Dispose();$raw=$null;$blocks=$null;$b=$null}
