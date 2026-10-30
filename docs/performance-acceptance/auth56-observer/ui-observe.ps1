$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='bili-auth56-cf825baf'
$art=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../artifacts/auth56'))
$exe=[IO.Path]::GetFullPath('C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1630ecee/artifacts/full-app-duration-fix/layout/BiliCopilot.UI.exe')
$owned=@(Get-Process | Where-Object {$_.Path -eq $exe})
if($owned.Count -ne 1 -or $owned[0].Id -eq 9608){throw 'Owned process mismatch'}
$appPid=$owned[0].Id
$w=[string]$owned[0].MainWindowHandle
function U([string[]]$a){
 $raw=@(& winapp ui @a --on local -w $w --json 2>$null)
 $code=$LASTEXITCODE
 if($a[0] -ne 'inspect'){ @{time=(Get-Date -Format o);pid=$appPid;hwnd=$w;args=$a;exit=$code}|ConvertTo-Json -Compress|Add-Content "$art/actions.jsonl" }
 if($code -ne 0){throw "UI failure $($a[0]); raw withheld"}
 ($raw -join "`n")|ConvertFrom-Json
}
function Nodes($ns){foreach($n in $ns){$n; Nodes $n.children}}
function Tree { $t=U @('inspect','--depth','25'); @(Nodes $t.windows.elements) }
function Safe {
 $ns=Tree
 $ns|Group-Object type,automationId|Select-Object Count,Name
 $ns|Where-Object {$_.name -cmatch '^(推荐|热门|全站排行|排行榜|流行|视频分区|分区|刷新|搜索|设置|加载中|正在加载|登录)$'}|Select-Object type,name,automationId,selector,isOffscreen,isEnabled
}
function Snap([string]$label){
 $ns=Tree
 $p=Get-Process -Id $appPid
 $cards=@($ns|Where-Object {$_.automationId -eq 'RootCard'})
 $viewport=@($ns|Where-Object {$_.automationId -eq 'PART_ScrollPresenter' -and $_.width -gt 700})|Select-Object -First 1
 $visible=@($cards|Where-Object {!$_.isOffscreen -and $_.width -gt 0 -and $_.height -gt 0 -and $_.x -lt ($viewport.x+$viewport.width) -and ($_.x+$_.width) -gt $viewport.x -and $_.y -lt ($viewport.y+$viewport.height) -and ($_.y+$_.height) -gt $viewport.y})
 $hashes=@(foreach($card in $cards){$txt=(@(Nodes $card.children)|Where-Object {$_.type -eq 'Text' -and $_.name -notmatch 'https?://'}|ForEach-Object {$_.name}) -join "`n"; [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($txt)))})
 $visibleHashes=@(foreach($card in $visible){$txt=(@(Nodes $card.children)|Where-Object {$_.type -eq 'Text' -and $_.name -notmatch 'https?://'}|ForEach-Object {$_.name}) -join "`n"; [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($txt)))})
 $s=[ordered]@{time=(Get-Date -Format o);label=$label;pid=$appPid;hwnd=$w;uiaNodes=$ns.Count;uiaRootCards=$cards.Count;uiaOnscreenRootCards=@($cards|Where-Object {!$_.isOffscreen}).Count;uiaViewportCards=$visible.Count;uiaDistinctTextHashes=@($hashes|Select-Object -Unique).Count;uiaSequenceHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($hashes -join '|'))));visibleHashes=$visibleHashes;visibleFirstY=if($visible.Count){$visible[0].y}else{$null};privateBytes=$p.PrivateMemorySize64;workingSetBytes=$p.WorkingSet64;actualItems=$null;realizedCards=$null;businessBatches=$null}
 $s|ConvertTo-Json -Compress|Add-Content "$art/samples.jsonl"
 $s
}
function Section([string]$name){
 $nodes=Tree
 $items=@($nodes|Where-Object {$_.type -eq 'ListItem' -and (@(Nodes $_.children)|Where-Object {$_.type -eq 'Text' -and $_.name -ceq $name}).Count -gt 0})
 if($items.Count -ne 1){throw 'Section selector ambiguous'}
 U @('click',$items[0].selector)|Out-Null
}
