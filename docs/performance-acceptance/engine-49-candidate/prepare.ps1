param([Parameter(Mandatory)][string]$Destination,[switch]$Candidate,[switch]$Barriers)
$ErrorActionPreference='Stop'
$repo=(git rev-parse --show-toplevel).Trim()
$destinationFull=[IO.Path]::GetFullPath($Destination)
$artifactRoot=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))+[IO.Path]::DirectorySeparatorChar
if(!$destinationFull.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Destination must be inside this worktree artifacts'}
if(Test-Path $destinationFull){throw 'Destination must be new'}
New-Item -ItemType Directory $destinationFull | Out-Null
$sha='4d0f8691f08f1e143294402477e72306a1b38b6f'
$archive=Join-Path $destinationFull 'upstream.zip'
Invoke-WebRequest "https://codeload.github.com/Richasy/danmaku-kernel/zip/$sha" -OutFile $archive
if((Get-FileHash $archive).Hash -ne '5B6465B63C438CDBFA8C0C2D7697719A677539522D98FEEB7C2B7B87F1691BC0'){throw 'Archive hash mismatch'}
Expand-Archive $archive (Join-Path $destinationFull 'source')
$source=Join-Path $destinationFull "source/danmaku-kernel-$sha"
function Apply([string]$Directory,[string]$Patch){
 $relative=[IO.Path]::GetRelativePath($repo,$Directory).Replace('\','/')
 & git -C $repo apply --ignore-whitespace "--directory=$relative" $Patch
 if($LASTEXITCODE -ne 0){throw "Patch failed: $Patch"}
}
if($Candidate){
 Apply $source "$PSScriptRoot/scheduler-candidate.patch"
 # Match the tested source bytes, including UTF-8 BOM and CRLF.
 foreach($f in 'Danmaku.Core/DanmakuScheduler.cs','Danmaku.Legacy/DanmakuFrostMaster.cs'){
  $p=Join-Path $source "src/$f"
  $text=[IO.File]::ReadAllText($p).Replace("`r`n","`n").Replace("`n","`r`n")
  [IO.File]::WriteAllText($p,$text,[Text.UTF8Encoding]::new($true))
 }
}
if($Barriers){& python "$PSScriptRoot/instrument.py" $source;if($LASTEXITCODE -ne 0){throw 'Instrumentation failed'}}
$harness=Join-Path $destinationFull 'harness'
& "$PSScriptRoot/../harness-49/prepare.ps1" -SourceRoot $repo -Destination $harness
Apply $harness "$PSScriptRoot/harness.patch"
(Get-Content "$harness/Package.appxmanifest" -Raw).Replace('Acceptance49C4D3','Acceptance49C9D5') | Set-Content "$harness/Package.appxmanifest"
$project=Get-Content "$harness/Harness.csproj" -Raw
foreach($name in 'Core','Legacy','Share'){
 $project=$project.Replace("<PackageReference Include=`"Danmaku.$name`" Version=`"0.0.1-preview6`" />","<ProjectReference Include=`"../source/danmaku-kernel-$sha/src/Danmaku.$name/Danmaku.$name.csproj`" />")
}
$project | Set-Content "$harness/Harness.csproj"
'<Project />' | Set-Content "$harness/Directory.Packages.props"
Copy-Item "$PSScriptRoot/EngineCases.cs" $harness
Write-Output "Prepared $harness; set NUGET_PACKAGES and NUGET_HTTP_CACHE_PATH under $destinationFull before build. Do not run EngineTests without -Barriers."
