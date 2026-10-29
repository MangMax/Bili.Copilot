$ErrorActionPreference='Stop'
$root=$PSScriptRoot
function ReadEvents($name){@(Get-Content "$root/$name/events.jsonl"|ForEach-Object{$_|ConvertFrom-Json})}
function Require($condition,$message){if(-not $condition){throw $message}}
$late=ReadEvents 'late';$life=ReadEvents 'lifecycle';$suite=ReadEvents 'suite';$mouse=ReadEvents 'mouse'
$unload=$late|Where-Object {$_.kind -eq 'state' -and $_.data.condition -eq 'unloaded'}|Select-Object -First 1
$attach=$late|Where-Object {$_.kind -eq 'pipe' -and $_.data -eq 'Attach'}|Select-Object -First 1
$window=@($late|Where-Object {$_.ms -gt $unload.ms -and $_.ms -lt $attach.ms})
Require (@($window|Where-Object kind -eq request).Count -eq 0) 'Request during whole unloaded window'
Require (@($window|Where-Object {$_.kind -in 'state','render' -and $_.data.items.Count -gt 0}).Count -eq 0) 'Publication during unloaded window'
$input=$window|Where-Object {$_.kind -eq 'text' -and $_.data.reason -eq 'UserInput' -and $_.data.Text -eq 'q'}
Require ($input -and -not $input.data.loaded) 'Missing late real input'
$keyword=$late|Where-Object {$_.kind -eq 'keyword' -and $_.data.keyword -eq 'q'}|Select-Object -First 1
Require ($keyword.ms -lt $unload.ms -and $input.ms -gt $unload.ms) 'Wrong ordering'
$firstLoaded=$suite|Where-Object {$_.kind -eq 'state' -and $_.data.condition -eq 'loaded'}|Select-Object -First 1
$firstText=$suite|Where-Object {$_.kind -eq 'text' -and $_.data.reason -eq 'UserInput'}|Select-Object -First 1
$firstRequest=$suite|Where-Object {$_.kind -eq 'request' -and $_.data.keyword -eq 'bili'}|Select-Object -First 1
Require ($firstLoaded.ms -lt $firstText.ms -and $firstText.data.loaded -and $firstRequest.ms -gt $firstText.ms) 'Initial input not accepted after Loaded'
$submit=$mouse|Where-Object {$_.kind -eq 'submit' -and $_.data.QueryText -eq '你好'}|Select-Object -Last 1
$fence=$mouse|Where-Object {$_.kind -eq 'state' -and $_.data.condition -like 'fence*'}|Select-Object -Last 1
Require ($submit -and $fence.ms-$submit.ms -ge 850) 'Mouse negative window missing'
$checks=@();foreach($name in 'late','lifecycle','suite','mouse'){$r=Get-Content "$root/$name/results.json"|ConvertFrom-Json;Require (@($r.checks|Where-Object {-not $_.passed}).Count -eq 0) "Failed case in $name";$checks+=@{run=$name;count=$r.checks.Count}}
Require (@(@($late)+@($life)+@($mouse)|Where-Object kind -eq unhandled).Count -eq 0) 'Unhandled exception'
$ownedIds=@(Get-ChildItem "$root/launch-*.json"|ForEach-Object{(Get-Content $_|ConvertFrom-Json).ProcessId})
Require (@($ownedIds|Where-Object {Get-Process -Id $_ -ErrorAction SilentlyContinue}).Count -eq 0) 'Owned process still running'
Require ($null -eq (Get-AppxPackage -Name BiliCopilot.Acceptance51Late751)) 'Owned package still registered'
@{sourceHead='7516070bd8b88be1bc024515db92ee3931ed6a43';checks=$checks;keywordMs=$keyword.ms;unloadedMs=$unload.ms;lateInputMs=$input.ms;unloadedObservationMs=$attach.ms-$unload.ms;lateRequests=0;latePublications=0;firstLoadedMs=$firstLoaded.ms;firstUserInputMs=$firstText.ms;firstInputRequestMs=$firstRequest.ms;mouseFenceAfterSubmitMs=$fence.ms-$submit.ms;ownedPids=$ownedIds;ownedProcessesGone=$true;ownedPackageGone=$true}|ConvertTo-Json -Depth 6|Set-Content "$root/audit.json"
Get-Content "$root/audit.json"
