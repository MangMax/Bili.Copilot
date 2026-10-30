# Read only the explicitly authorized package log. Never emit exception messages or raw blocks.
[CmdletBinding()]
param(
    [ValidatePattern('^\d{2}:\d{2}:\d{2}\.\d{3}$')][string]$StartTime='12:45:00.000',
    [ValidatePattern('^\d{2}:\d{2}:\d{2}\.\d{3}$')][string]$EndTime='13:08:00.000'
)
$ErrorActionPreference = 'Stop'
if ($StartTime -ge $EndTime) { throw 'Expected a nonempty same-day time window' }
$package = Get-AppxPackage -Name 'Mang.BiliCopilot.PerfAcceptance.Caa6a7be'
$family = 'Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg'
if (@($package).Count -ne 1 -or $package.PackageFamilyName -ne $family) { throw 'Package identity mismatch' }
$path = Join-Path $env:LOCALAPPDATA "Packages/$family/LocalState/Logger/log-2026-10-30.txt"
$stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
$reader = [IO.StreamReader]::new($stream)
function Get-BlockHash([string]$value) {
    # Hash decoded UTF-8 text, retaining original newlines; not a file-byte hash.
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($value)))
}
$allowedTypes = @('RichasyKernel.KernelException', 'System.Threading.Tasks.TaskCanceledException',
    'System.IO.IOException', 'System.Net.Sockets.SocketException', 'System.TimeoutException',
    'System.OperationCanceledException', 'System.Net.Http.HttpRequestException',
    'Google.Protobuf.InvalidProtocolBufferException', 'System.Text.Json.JsonException', 'System.Exception')
# These method identifiers were verified against the checked-in production source. Never emit signatures.
$allowedMethods = @('ThrowIfResponseInvalidAsync', 'SendAsync', 'GetSegmentDanmakusWithGrpcAsync',
    'GetSegmentDanmakusWithWebAsync', 'LoadSegmentAsync')
try {
    $raw = $reader.ReadToEnd()
    $blocks = [regex]::Split($raw, '(?m)(?=^2026-10-30 \d{2}:\d{2}:\d{2}\.\d{3})')
    $rows = @(foreach ($block in $blocks) {
        if ($block -notmatch '^2026-10-30 (\d{2}:\d{2}:\d{2}\.\d{3})') { continue }
        $time = $Matches[1]
        if ($time -lt $StartTime -or $time -ge $EndTime) { continue }
        $header = ($block -split '\r?\n', 2)[0]
        $segment = $header.Contains('获取弹幕分片失败')
        $types = @([regex]::Matches($block, '(?m)^\s*(?:---> )?((?:RichasyKernel|System|Google\.Protobuf)\.[A-Za-z.]+Exception)(?:[: (]|$)') | ForEach-Object {
            $type = $_.Groups[1].Value
            if ($type -in $allowedTypes) { $type } else { 'UNCLASSIFIED_EXCEPTION_TYPE' }
        })
        if (!$segment -and !$types.Count) { continue }
        $category = if ($segment) { 'danmaku-segment' } elseif ($header.Contains('搜索建议')) { 'search-suggestion' } else { 'other' }
        # Exact fixed literal from BiliHttpClient.Extension.cs:40,49. No remote text escapes.
        $kind = if ($segment -and $block -match '(?m)^RichasyKernel\.KernelException: 哔哩哔哩返回了一个空的响应\r?$') { 'kernel-empty-response' } else { 'not-classified' }
        $methods = @($allowedMethods | Where-Object { $block -match ('\.' + [regex]::Escape($_) + '(?:\(|>)') })
        # Only explicitly labelled numeric values, never arbitrary numbers in remote text.
        $codes = @([regex]::Matches($block, '(?m)^\s*(?:---> )?System\.Net\.Sockets\.SocketException \((-?\d{1,10})\)') | ForEach-Object { [long]$_.Groups[1].Value })
        [pscustomobject]@{ time=$time; category=$category; kind=$kind; chain=($types -join ' > ');
            methods=($methods -join ','); numericCodes=($codes -join ','); hash=(Get-BlockHash $block) }
    })
    $groups = @($rows | Group-Object category,kind,chain,methods,numericCodes | ForEach-Object {
        $first=$_.Group[0]; $last=$_.Group[-1]
        [ordered]@{ category=$first.category; kind=$first.kind; exceptionChain=$first.chain;
            methods=$first.methods; numericCodes=$first.numericCodes; count=$_.Count;
            firstTime=$first.time; lastTime=$last.time; firstBlockSha256=$first.hash; lastBlockSha256=$last.hash;
            orderedBlockHashesSha256=(Get-BlockHash (($_.Group.hash) -join "`n")) }
    })
    [ordered]@{ schema=1; packageFamily=$family; window="2026-10-30 [$StartTime,$EndTime) UTC+08";
        boundary='Serilog timestamp header through before next timestamp header';
        hashDefinition='SHA256 UTF8 decoded block text; aggregate hashes joined by LF';
        groups=$groups;
        segmentMinuteCounts=@($rows | Where-Object category -eq 'danmaku-segment' | Group-Object { $_.time.Substring(0,5) } | ForEach-Object { [ordered]@{minute=$_.Name;count=$_.Count} });
        note='Exception chains are serialized outer-to-inner type order; no request correlation or response payload retained.'
    } | ConvertTo-Json -Depth 8
} finally {
    $reader.Dispose(); $stream.Dispose(); $raw=$null; $blocks=$null; $block=$null; $header=$null
}
