$ErrorActionPreference = 'Stop'
$kernel = Join-Path $PSScriptRoot '../../../externals/bili-kernel'
$sha = git -C $kernel rev-parse HEAD
if ($sha -ne '2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d') { throw "Unexpected kernel: $sha" }
if (git -C $kernel status --porcelain --untracked-files=no) { throw 'Kernel has source changes; aborting' }
$destination = Join-Path $PSScriptRoot 'obj/candidate'
New-Item -ItemType Directory -Force $destination | Out-Null
foreach ($name in @('BiliHttpClient.cs', 'BiliHttpClient.Extension.cs')) {
    $source = Get-Content (Join-Path $kernel "src/BiliKernel.Core/Http/$name") -Raw
    # Temporary experiment only: same transport, validation, catches and ownership.
    $candidate = $source.Replace('BiliHttpClient', 'CandidateHttpClient')
    if ($name -eq 'BiliHttpClient.cs') {
        $needle = 'parser.ParseFrom([.. bytes.Skip(5)])'
        if (-not $candidate.Contains($needle)) { throw 'Baseline expression changed' }
        # Preserve direct ParseAsync behavior for <5 bytes (Skip yields empty).
        $candidate = $candidate.Replace($needle, 'parser.ParseFrom(bytes, Math.Min(5, bytes.Length), Math.Max(0, bytes.Length - 5))')
    }
    Set-Content (Join-Path $destination $name) $candidate -Encoding utf8
}
