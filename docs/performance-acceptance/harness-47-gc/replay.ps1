param([Parameter(Mandatory)][string]$SourceRoot,[Parameter(Mandatory)][string]$Scratch,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
if((Test-Path $Scratch) -or (Test-Path $Evidence)){throw 'Use fresh scratch and evidence roots'}
New-Item -ItemType Directory $Scratch,$Evidence | Out-Null
dotnet build "$PSScriptRoot/Trace/Trace.csproj" -c Release --source "$env:USERPROFILE/.nuget/packages"
if($LASTEXITCODE -ne 0){throw 'Collector build failed'}
$trace=(Resolve-Path "$PSScriptRoot/Trace/bin/Release/net10.0/Trace.exe").Path
$prefix=[IO.Path]::GetFullPath("$Scratch/probe")
$probe=Start-Process $trace -ArgumentList @('probe',$prefix) -PassThru -WindowStyle Hidden
& $trace collect $probe.Id $prefix (Split-Path $trace)
if($LASTEXITCODE -ne 0){throw 'Probe collection failed'}
$probe.WaitForExit()
& $trace parse "$prefix.nettrace" "$Evidence/probe-events.json"
if($LASTEXITCODE -ne 0){throw 'Probe parsing failed'}
$probeEvents=Get-Content "$Evidence/probe-events.json" -Raw|ConvertFrom-Json
if($probeEvents.lost -ne 0 -or @($probeEvents.events|Where-Object name -eq 'GC/SuspendEEStart').Count -eq 0){throw 'Probe did not establish usable events'}
foreach($mode in @('current','historical')) {
 & "$PSScriptRoot/prepare.ps1" -SourceRoot $SourceRoot -Destination "$Scratch/$mode" -Mode $mode
 dotnet build "$Scratch/$mode/Harness.csproj" -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=false --source "$env:USERPROFILE/.nuget/packages" > "$Evidence/build-$mode.txt"
 if($LASTEXITCODE -ne 0){throw "Build $mode failed"}
}
$modes=@('current','historical','historical','current','current','historical','current','historical')
for($i=0;$i -lt $modes.Count;$i++) {
 $n=$i+1;$mode=$modes[$i]
 & "$PSScriptRoot/run.ps1" -Project "$Scratch/$mode/Harness.csproj" -Layout "$Scratch/layout-$n" -Evidence "$Evidence/final$n-$mode" -TracePrefix "$Scratch/trace-$n" -CaptureLifecycle:($n -in @(1,2,7)) -NoTrace:($n -gt 6) -ScreenCapture:($n -gt 6)
 if($LASTEXITCODE -ne 0){throw "Run $n failed"}
}
python "$PSScriptRoot/analyze.py" $Evidence
if($LASTEXITCODE -ne 0){throw 'Analysis failed'}
python "$PSScriptRoot/verify.py" $Evidence
if($LASTEXITCODE -ne 0){throw 'Verification failed'}
