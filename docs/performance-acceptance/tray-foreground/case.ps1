param([ValidateSet('Mouse','Invoke','Aumid','Menu')][string]$Case,[string]$Run='run1',[switch]$NativeClose,[switch]$CaptionMouse)
. "$PSScriptRoot/ui.ps1" -Run $Run
if(Test-Path "$out/case-$Case.json"){throw 'Fresh case path required; preserve prior evidence'}
$caseStart=[DateTime]::UtcNow.ToString('o');$failure=$null;$restored=$null;$value=$null
$token='tray'+$Case.ToLowerInvariant()
try{
 if($NativeClose){
  U @('set-value','KeyboardReceipt','')|Out-Null
  Mouse $W 'KeyboardReceipt'
  $tree=U @('inspect','--depth','4');$close=@(Nodes $tree.windows.elements|Where-Object {$_.name -eq 'Close' -and $_.automationId -eq 'Close'})
  if($close.Count -ne 1){throw 'PRECONDITION: exact system Close required'}
  if($CaptionMouse){Mouse $W $close[0].selector}else{U @('invoke',$close[0].selector)|Out-Null}
 }else{U @('invoke','HideOwned')|Out-Null}
 $c=Get-Content "$out/competitor-ready.json" -Raw|ConvertFrom-Json
 Mouse ([string]$c.hwnd) '101'
 $deadline=[DateTime]::UtcNow.AddSeconds(3)
 do{$before=State "$Case-competitor";if($before.foreground -eq $c.hwnd -and !$before.visible){break};Start-Sleep -Milliseconds 50}while([DateTime]::UtcNow -lt $deadline)
 if($before.foreground -ne $c.hwnd -or $before.visible){throw 'PRECONDITION: owned competitor must be foreground and main hidden'}
 # Allow shell dismissal animation to finish before opening another popup; not an acceptance verdict.
 Start-Sleep -Milliseconds 300
 if($Case -eq 'Aumid'){
  $start=[DateTime]::UtcNow.ToString('o');Start-Process explorer.exe -ArgumentList "shell:AppsFolder\$($r.aumid)"
  @{startUtc=$start;endUtc=[DateTime]::UtcNow.ToString('o');args=@('aumid',$r.aumid)}|ConvertTo-Json -Compress|Add-Content "$out/actions.jsonl"
 }else{
  $tray=OpenTray ($Case -eq 'Invoke')
  if($Case -eq 'Invoke'){U @('invoke',$tray.node.selector) $tray.hwnd|Out-Null}
  elseif($Case -eq 'Menu'){
   Mouse $tray.hwnd $tray.node.selector $true
   $deadline=[DateTime]::UtcNow.AddSeconds(3)
   do{$wins=winapp ui list-windows --on local -a $r.pid --json|ConvertFrom-Json;$menus=@($wins|Where-Object className -eq '#32768');if(!$menus.Count){Start-Sleep -Milliseconds 100}}while(!$menus.Count -and [DateTime]::UtcNow -lt $deadline)
   if($menus.Count -ne 1){throw 'PRECONDITION: one native owned menu required'}
   $tree=winapp ui inspect --depth 5 --on local -w $menus[0].hwnd --json|ConvertFrom-Json
   $show=@(Nodes $tree.windows.elements|Where-Object {$_.name -eq '显示主窗口'})
   if($show.Count -ne 1){throw 'PRECONDITION: exact Show Main menu item required'}
   Mouse ([string]$menus[0].hwnd) $show[0].selector
  }else{Mouse $tray.hwnd $tray.node.selector}
 }
 $deadline=[DateTime]::UtcNow.AddSeconds(3)
 do{$restored=State "$Case-restored";if($restored.visible -and !$restored.minimized -and $restored.targetForeground){break};Start-Sleep -Milliseconds 50}while([DateTime]::UtcNow -lt $deadline)
 # Evaluate all three independently. Never substitute Invoke success or an injected character count.
 $visiblePass=$restored.visible -and !$restored.minimized
 $foregroundPass=$restored.targetForeground
 $inputError=$null
 try{U @('send-keys',$token,'--verbatim','--via','send-input','--target','KeyboardReceipt')|Out-Null}catch{$inputError="$_"}
 $value=U @('get-value','KeyboardReceipt')
 $keyboardPass=$value.text -ceq $token
 if(!$visiblePass -or !$foregroundPass -or !$keyboardPass){throw "visible=$visiblePass foreground=$foregroundPass keyboard=$keyboardPass inputError=$inputError"}
}catch{$failure="$_"}
@{case=$Case;nativeClose=[bool]$NativeClose;captionMouse=[bool]$CaptionMouse;startUtc=$caseStart;endUtc=[DateTime]::UtcNow.ToString('o');before=$before;restored=$restored;visiblePass=$visiblePass;foregroundPass=$foregroundPass;keyboardPass=$keyboardPass;value=$value;failure=$failure;pass=($null -eq $failure)}|ConvertTo-Json -Depth 6|Set-Content "$out/case-$Case.json"
if($failure){throw $failure}
Write-Output "$Case PASS: visible, foreground, real keyboard receipt"
