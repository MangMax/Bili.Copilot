$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='bili-net11-4d615e72'
$evidence=$PSScriptRoot
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
function Bind([string]$run='run1'){
 $r=Get-Content "$evidence/$run/ready.json" -Raw | ConvertFrom-Json
 $p=Get-Process -Id $r.pid
 if($p.Path -ne $r.exe -or $p.StartTime.ToUniversalTime().ToFileTimeUtc() -ne $r.creationFileTime){throw 'Target identity mismatch'}
 $script:AppPid=$p.Id; $script:W=[string]$p.MainWindowHandle
}
function U([string[]]$a){
 $raw=@(& winapp ui @a --on local -w $script:W --json 2>$null); $code=$LASTEXITCODE
 if($a[0] -ne 'inspect'){@{utc=[DateTime]::UtcNow.ToString('o');pid=$AppPid;hwnd=$W;args=$a;code=$code}|ConvertTo-Json -Compress|Add-Content "$evidence/actions.jsonl"}
 if($code -ne 0){throw "UI $($a[0]) failed ($code), raw withheld"}
 ($raw -join "`n")|ConvertFrom-Json
}
function Nodes($ns){foreach($n in $ns){$n;Nodes $n.children}}
function Tree { $t=U @('inspect','--depth','22'); @(Nodes $t.windows.elements) }
$safePattern='^(推荐|热门|全站排行|排行榜|流行|视频分区|分区|刷新|撤销|搜索|设置|加载中|正在加载|登录|退出|关闭|分集|信息|弹幕设置|无弹幕|连接中|已加载 [\d,]+ 条弹幕|Python导学视频|第一阶段-第一章-.*|\d{1,2}:\d{2}(:\d{2})?|主页|番剧|国创|取消|确定|保存|外观|播放|常规)$'
function Safe {
 $ns=Tree
 $ns | Where-Object {$_.automationId -or $_.name -cmatch $safePattern} | Select-Object type,automationId,selector,isEnabled,isOffscreen,x,y,width,height,@{n='safeName';e={if($_.name -cmatch $safePattern){$_.name}else{''}}}
}
function Act([string]$s){
 $n=@(Tree|Where-Object {$_.selector -eq $s -or $_.automationId -eq $s -or $_.name -ceq $s})
 if($n.Count -ne 1 -or !$n[0].isEnabled -or $n[0].isOffscreen){throw 'Control not uniquely enabled/visible'}
 U @('invoke',$s)|Out-Null
}
function Snap([string]$label){
 $ns=Tree; $p=Get-Process -Id $AppPid
 $cards=@($ns|Where-Object automationId -eq RootCard)
 $hashes=@(foreach($c in $cards){$txt=(@(Nodes $c.children)|Where-Object type -eq Text|ForEach-Object name)-join "`n";[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($txt)))})
 $s=@{utc=[DateTime]::UtcNow.ToString('o');label=$label;pid=$AppPid;hwnd=$W;rootCards=$cards.Count;sequenceHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($hashes -join '|'))));loginMarker=@($ns|Where-Object {$_.name -ceq '登录'}).Count;privateBytes=$p.PrivateMemorySize64;workingSet=$p.WorkingSet64;cpuMs=$p.TotalProcessorTime.TotalMilliseconds}
 $s|ConvertTo-Json -Compress|Add-Content "$evidence/samples.jsonl"; $s
}
