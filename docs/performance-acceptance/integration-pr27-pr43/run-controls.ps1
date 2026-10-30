param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='integration2743-latest'
$Layout=[IO.Path]::GetFullPath($Layout)
$Evidence=[IO.Path]::GetFullPath($Evidence)
$package=Get-AppxPackage -Name BiliCopilot.Integration2743.Controls
if(!$package -or $package.InstallLocation -ne $Layout -or (Get-Process -Id $AppPid).Path -ne "$Layout\Harness.exe"){throw 'Not owned controls host'}
New-Item -ItemType Directory -Force $Evidence | Out-Null
$state=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
$resultFile=Join-Path $state 'integration-results.json'
if(Test-Path -LiteralPath $resultFile){Copy-Item $resultFile "$Evidence/previous-results.json"; Remove-Item -LiteralPath $resultFile}
function UI([string[]]$Arguments){$out=& winapp ui @Arguments --on local -a $AppPid --json; if($LASTEXITCODE -ne 0){throw ($out -join "`n")}; $out}
try {
 UI @('list-windows') | Set-Content "$Evidence/windows.json"
 UI @('invoke','IntegrationChecks') | Set-Content "$Evidence/invoke.json"
 $deadline=[DateTime]::UtcNow.AddSeconds(45)
 while(!(Test-Path $resultFile) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 250}
 if(!(Test-Path $resultFile)){throw 'Native checks did not finish'}
 Copy-Item $resultFile "$Evidence/results.json"
 UI @('screenshot','--output',"$Evidence/final.png") | Set-Content "$Evidence/screenshot.json"
 UI @('inspect','--depth','8') | Set-Content "$Evidence/tree.json"
 Copy-Item "$state/trace.jsonl" "$Evidence/trace.jsonl"
 $results=Get-Content "$Evidence/results.json" -Raw|ConvertFrom-Json
 $results | Format-Table name,status,detail -Wrap
 if(@($results|Where-Object status -eq FAIL).Count){exit 1}
} finally { & winapp ui yield --on local --json | Set-Content "$Evidence/yield.json" }
