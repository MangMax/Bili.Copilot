# Read-only pixel verification; does not create or modify any image.
Add-Type -AssemblyName System.Drawing
$results = foreach ($folder in 'run', 'replay-original') {
    foreach ($file in Get-ChildItem "artifacts/engine49-trace/$folder/*.png") {
        $bitmap = [Drawing.Bitmap]::new($file.FullName)
        try {
            $bytes = [Collections.Generic.List[byte]]::new()
            $white = 0
            for ($y = 320; $y -lt 800; $y++) {
                for ($x = 20; $x -lt 1360; $x++) {
                    $c = $bitmap.GetPixel($x, $y)
                    $bytes.Add($c.R); $bytes.Add($c.G); $bytes.Add($c.B)
                    if ($c.R -gt 150 -and $c.G -gt 150 -and $c.B -gt 150) { $white++ }
                }
            }
            @{folder=$folder;file=$file.Name;renderRoi='x20..1359,y320..799';whitePixels=$white;roiSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes.ToArray()))}
        } finally { $bitmap.Dispose() }
    }
}
$results | ConvertTo-Json -Depth 4
