param([Parameter(Mandatory)][string]$Archive, [Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$repo = (git rev-parse --show-toplevel).Trim()
$destinationFull = [IO.Path]::GetFullPath($Destination)
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (!$destinationFull.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use this workspace artifacts' }
if (Test-Path $destinationFull) { throw 'Destination must be new; no incremental reuse' }
if ((Get-FileHash $Archive).Hash -ne '5B6465B63C438CDBFA8C0C2D7697719A677539522D98FEEB7C2B7B87F1691BC0') { throw 'Wrong source archive' }
$sha = '4d0f8691f08f1e143294402477e72306a1b38b6f'
function Apply([string]$Target, [string]$Patch) {
    $relative = [IO.Path]::GetRelativePath($repo, $Target).Replace('\', '/')
    git -C $repo apply --ignore-whitespace "--directory=$relative" $Patch
    if ($LASTEXITCODE -ne 0) { throw "Failed patch: $Patch" }
}
foreach ($variant in 'original', 'candidate', 'trace') {
    Expand-Archive -LiteralPath $Archive -DestinationPath "$destinationFull/$variant"
    if ($variant -ne 'original') {
        Apply "$destinationFull/$variant/danmaku-kernel-$sha" "$PSScriptRoot/../engine-49-candidate/scheduler-candidate.patch"
        # Match built bytes: CRLF; Legacy retains its original BOM, Core has none.
        foreach ($file in 'Danmaku.Core/DanmakuScheduler.cs', 'Danmaku.Legacy/DanmakuFrostMaster.cs') {
            $path = "$destinationFull/$variant/danmaku-kernel-$sha/src/$file"
            $text = [IO.File]::ReadAllText($path).Replace("`r`n", "`n").Replace("`n", "`r`n")
            [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($file.StartsWith('Danmaku.Legacy/')))
        }
    }
}
python "$PSScriptRoot/instrument.py" "$destinationFull/trace/danmaku-kernel-$sha"
if ($LASTEXITCODE -ne 0) { throw 'Instrumentation failed' }
& "$PSScriptRoot/../harness-49/prepare.ps1" -SourceRoot $repo -Destination "$destinationFull/harness"
Apply "$destinationFull/harness" "$PSScriptRoot/../engine-49-candidate/harness.patch"
Copy-Item "$PSScriptRoot/../engine-49-candidate/EngineCases.cs" "$destinationFull/harness"
python "$PSScriptRoot/prepare-host.py" "$destinationFull/harness"
if ($LASTEXITCODE -ne 0) { throw 'Host preparation failed' }
