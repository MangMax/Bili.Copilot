param([string]$Run='runtime',[string]$Identity='Bili.Remount6273',[string]$Layout='layout')
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='remount-6273-full'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'full'))
$identity=$Identity
if(Get-AppxPackage -Name $identity){throw 'Owned identity must be new'}
$evidence=Join-Path $root $Run
New-Item -ItemType Directory $evidence -ErrorAction Stop | Out-Null
$started=[DateTime]::UtcNow
$args224=@('run',"$root/src/_build/x64/Release/BiliCopilot.UI/bin",'--manifest',"$root/src/_build/x64/Release/BiliCopilot.UI/bin/AppxManifest.xml",'--exe','BiliCopilot.UI.exe','--output-appx-directory',"$root/$Layout",'--on','local','--detach','--json')
$raw=@(& winapp @args224);$launchCode=$LASTEXITCODE
$raw | Set-Content "$evidence/launch.json"
if($launchCode){throw "launch exit $launchCode"}
$launch=($raw -join "`n")|ConvertFrom-Json
$ownedPid=[int]$launch.ProcessId
$target=Get-Process -Id $ownedPid
$handle=$target.Handle
$path=$target.MainModule.FileName
if(!$path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Foreign process path'}
@{pid=$ownedPid;path=$path;creation=$target.StartTime.ToUniversalTime();handle=$handle.ToString();retained=$true}|ConvertTo-Json|Set-Content "$evidence/identity.json"
Write-Output "Owned process $ownedPid retained at $path"
$target.WaitForExit()
$target.Refresh();$exit=$target.ExitTime.ToUniversalTime()
@{pid=$ownedPid;path=$path;exit=$exit;exitCode=$target.ExitCode;retained=$true;forced=$false}|ConvertTo-Json|Set-Content "$evidence/retainedexit.json"
$delay=($exit.AddSeconds(31)-[DateTime]::UtcNow).TotalMilliseconds
if($delay -gt 0){Start-Sleep -Milliseconds ([int]$delay)}
$query=[DateTime]::UtcNow
Get-WinEvent -ListLog Application -ErrorAction Stop|Out-Null
$events=@();try{$events=@(Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=$started.ToLocalTime();Id=1000,1001} -ErrorAction Stop)}catch{if($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*'){throw}}
$matches=@($events|Where-Object{([xml]$_.ToXml()).Event.EventData.Data|Where-Object{ $_.InnerText -eq $path -or $_.InnerText -eq [string]$ownedPid -or $_.InnerText -eq ('0x{0:x}' -f $ownedPid)}}|ForEach-Object{@{id=$_.Id;xml=$_.ToXml()}})
@{query=$query;secondsAfterExit=($query-$exit).TotalSeconds;applicationLogReadable=$true;matches=$matches}|ConvertTo-Json -Depth 8|Set-Content "$evidence/wer.json"
Write-Output 'Retained exit and delayed exact PID/path WER saved. Package cleanup remains separate.'
