param([ValidateSet('Mouse','Invoke','Aumid')][string]$Case)
. "$PSScriptRoot/ui.ps1" -Run logged1
if(Test-Path "$out/case-$Case.json"){throw 'Fresh case path required; preserve prior evidence'}
$start=[DateTime]::UtcNow.ToString('o');$failure=$null;$token='tray'+$Case.ToLowerInvariant();$inputError=$null
try{
 # Exact public header search box; refuse to overwrite prior user text.
 $value=winapp ui get-value TextBox --on local -w $W --json|ConvertFrom-Json
 if($LASTEXITCODE -ne 0 -or ![string]::IsNullOrEmpty($value.text)){throw 'PRECONDITION: header search must be empty'}
 Mouse $W 'TextBox'
 $t=U @('inspect','--depth','4');$close=@(Nodes $t.windows.elements|Where-Object {$_.name -eq 'Close' -and $_.automationId -eq 'Close'})
 if($close.Count -ne 1){throw 'PRECONDITION: exact system close required'}
 U @('invoke',$close[0].selector)|Out-Null
 $c=Get-Content "$out/competitor-ready.json" -Raw|ConvertFrom-Json;Mouse ([string]$c.hwnd) '101'
 Start-Sleep -Milliseconds 300
 $before=State "$Case-competitor"
 if($before.foreground -ne $c.hwnd -or $before.visible){throw 'PRECONDITION: competitor foreground / main hidden required'}
 if($Case -eq 'Aumid'){
  $at=[DateTime]::UtcNow.ToString('o');Start-Process explorer.exe -ArgumentList "shell:AppsFolder\$($r.aumid)"
  @{startUtc=$at;endUtc=[DateTime]::UtcNow.ToString('o');args=@('aumid',$r.aumid)}|ConvertTo-Json -Compress|Add-Content "$out/actions.jsonl"
 }else{
  $tray=OpenTray ($Case -eq 'Invoke')
  if($Case -eq 'Invoke'){U @('invoke',$tray.node.selector) $tray.hwnd|Out-Null}else{Mouse $tray.hwnd $tray.node.selector}
 }
 $deadline=[DateTime]::UtcNow.AddSeconds(3)
 do{$restored=State "$Case-restored";if($restored.visible -and !$restored.minimized -and $restored.targetForeground){break};Start-Sleep -Milliseconds 50}while([DateTime]::UtcNow -lt $deadline)
 $visiblePass=$restored.visible -and !$restored.minimized;$foregroundPass=$restored.targetForeground
 try{U @('send-keys',$token,'--verbatim','--via','send-input','--target','TextBox')|Out-Null}catch{$inputError="$_"}
 $actual=U @('get-value','TextBox');$keyboardPass=$actual.text -ceq $token
 if($keyboardPass){U @('send-keys','ctrl+a backspace','--via','send-input')|Out-Null;U @('send-keys','esc','--via','send-input')|Out-Null}
 if(!$visiblePass -or !$foregroundPass -or !$keyboardPass){throw "visible=$visiblePass foreground=$foregroundPass keyboard=$keyboardPass input=$inputError"}
}catch{$failure="$_"}
@{case=$Case;startUtc=$start;endUtc=[DateTime]::UtcNow.ToString('o');before=$before;restored=$restored;visiblePass=$visiblePass;foregroundPass=$foregroundPass;keyboardPass=$keyboardPass;inputError=$inputError;failure=$failure;pass=($null -eq $failure)}|ConvertTo-Json -Depth 6|Set-Content "$out/case-$Case.json"
if($failure){throw $failure}
Write-Output "$Case PASS: visible, foreground, real keyboard receipt; test text cleared without submit"
