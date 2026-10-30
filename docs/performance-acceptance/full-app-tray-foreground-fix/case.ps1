param([ValidateSet('History','Mouse','Menu','Physical')][string]$Case,[switch]$ResumeAfterClose,[string]$Attempt="")
. "$PSScriptRoot/ui.ps1" -Run logged1
$resultName=if($ResumeAfterClose){"$Case-resumed"}else{$Case}
$resultName += $Attempt
if(Test-Path "$out/case-$resultName.json"){throw 'Fresh result required'}
$start=[DateTime]::UtcNow.ToString('o');$failure=$null;$visiblePass=$false;$foregroundPass=$false;$keyboardPass=$false
try {
 if($ResumeAfterClose){
  if($Case -ne 'History'){throw 'Only historical close can resume'}
 }elseif($Case -eq 'History'){
  $pw=[string](Get-Content "$out/player-window.json" -Raw|ConvertFrom-Json).hwnd
  $ns=@(Nodes (U @('inspect','--depth','5') $pw).windows.elements)
  $close=@($ns|Where-Object {$_.automationId -eq 'Close' -and $_.name -eq 'Close' -and !$_.isOffscreen})
  if($close.Count -ne 1){throw 'Unique player caption close required'}
  U @('invoke',$close[0].selector) $pw|Out-Null
 }else{
  $value=U @('get-value','TextBox');if($value.text){throw 'Search must be empty'}
  Mouse $W 'TextBox'
  $ns=@(Nodes (U @('inspect','--depth','5')).windows.elements)
  $close=@($ns|Where-Object {$_.automationId -eq 'Close' -and $_.name -eq 'Close' -and !$_.isOffscreen})
  if($close.Count -ne 1){throw 'Unique main caption close required'}
  if($Case -eq 'Physical'){Mouse $W $close[0].selector}else{U @('invoke',$close[0].selector)|Out-Null}
 }
 $c=Get-Content "$out/competitor-ready.json" -Raw|ConvertFrom-Json
 Mouse ([string]$c.hwnd) '101'
 Start-Sleep -Milliseconds 300
 $before=State "$Case-precondition"
 if($before.foreground -ne $c.hwnd -or $before.visible){throw 'Main hidden and competitor foreground required'}
 $tray=OpenTray $true
 if($Case -eq 'Menu'){
  Mouse $tray.hwnd $tray.node.selector $true
  $deadline=[DateTime]::UtcNow.AddSeconds(3)
  do{$wins=winapp ui list-windows --on local -a $r.pid --json|ConvertFrom-Json;if($LASTEXITCODE){throw 'Menu discovery failed'};$menus=@($wins|Where-Object className -eq '#32768');if(!$menus.Count){Start-Sleep -Milliseconds 100}}while(!$menus.Count -and [DateTime]::UtcNow -lt $deadline)
  if($menus.Count -ne 1){throw 'One owned menu required'}
  $ns=@(Nodes (U @('inspect','--depth','5') ([string]$menus[0].hwnd)).windows.elements)
  $show=@($ns|Where-Object name -eq '显示主窗口');if($show.Count -ne 1){throw 'One Show menu required'}
  Mouse ([string]$menus[0].hwnd) $show[0].selector
 }else{Mouse $tray.hwnd $tray.node.selector}
 $deadline=[DateTime]::UtcNow.AddSeconds(3)
 do{$restored=State "$Case-independent-foreground";if($restored.visible -and !$restored.minimized -and $restored.targetForeground){break};Start-Sleep -Milliseconds 50}while([DateTime]::UtcNow -lt $deadline)
 $visiblePass=$restored.visible -and !$restored.minimized;$foregroundPass=$restored.targetForeground
 if(!$visiblePass -or !$foregroundPass){throw "visible=$visiblePass foreground=$foregroundPass; no keyboard tool permitted"}
 $v=U @('get-value','TextBox');if($v.text){U @('set-value','TextBox','')|Out-Null}
 if(!(State 'immediately-before-keyboard').targetForeground){throw 'Foreground lost before keyboard'}
 $token='tray'+$Case.ToLowerInvariant();U @('send-keys',$token,'--verbatim','--via','send-input','--target','TextBox')|Out-Null
 $v=U @('get-value','TextBox');$keyboardPass=$v.text -ceq $token
 U @('send-keys','ctrl+a backspace','--via','send-input')|Out-Null;U @('send-keys','esc','--via','send-input')|Out-Null
 $cleared=(U @('get-value','TextBox')).text -eq ''
 if(!$keyboardPass -or !$cleared){throw 'Keyboard receipt or clear failed'}
}catch{$failure="$_"}
@{case=$Case;startUtc=$start;endUtc=[DateTime]::UtcNow.ToString('o');before=$before;restored=$restored;visiblePass=$visiblePass;foregroundPass=$foregroundPass;keyboardPass=$keyboardPass;searchCleared=$cleared;failure=$failure;pass=($null -eq $failure)}|ConvertTo-Json -Depth 8|Set-Content "$out/case-$resultName.json"
if($failure){throw $failure}
Write-Output "$Case PASS"
