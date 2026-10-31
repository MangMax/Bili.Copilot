param([Parameter(Mandatory)][string]$Stage)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../..")
$out=Join-Path $root "artifacts/list-p1/build-$Stage"
if(Test-Path "$out.json"){throw 'Fresh build stage required'}
$start=Get-Date
& dotnet build "$root/artifacts/list-p1/host/Harness.csproj" -t:Rebuild -c Release -p:Platform=x64 -p:NuGetAudit=false *> "$out.log"
$code=$LASTEXITCODE
@{startUtc=$start.ToUniversalTime().ToString('O');endUtc=(Get-Date).ToUniversalTime().ToString('O');exitCode=$code} | ConvertTo-Json | Set-Content "$out.json"
if($code -ne 0){Get-Content "$out.log" -Tail 20; exit $code}
