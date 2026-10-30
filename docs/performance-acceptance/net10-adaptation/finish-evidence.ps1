param()
$ErrorActionPreference='Stop'
foreach($script in 'compare-closures.py','summarize.py','chart.py','archive.py'){
 & python "$PSScriptRoot/$script"
 if($LASTEXITCODE -ne 0){throw "$script failed"}
}
& python "$PSScriptRoot/verify.py" --live | Set-Content "$PSScriptRoot/verification.json"
if($LASTEXITCODE -ne 0){throw 'Evidence/DLL verification failed'}
Get-Content "$PSScriptRoot/verification.json"
