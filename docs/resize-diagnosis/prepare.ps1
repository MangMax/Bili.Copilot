param([string]$Destination)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../..")
if(!$Destination){$Destination="$root/artifacts/resize-diagnosis/host"}
& "$root/docs/performance-acceptance/integration-pr27-pr43/prepare-harness.ps1" -Destination $Destination
$archived="$root/docs/performance-acceptance/net10-adaptation/evidence/controls10/Harness.csproj.gz"
$inputStream=[IO.File]::OpenRead($archived)
$gzip=[IO.Compression.GZipStream]::new($inputStream,[IO.Compression.CompressionMode]::Decompress)
$reader=[IO.StreamReader]::new($gzip)
try{$project=$reader.ReadToEnd()}finally{$reader.Dispose();$gzip.Dispose();$inputStream.Dispose()}
$project.Replace('net10.0-windows','net11.0-windows') | Set-Content "$Destination/Harness.csproj"
$manifest=Get-Content "$Destination/Package.appxmanifest" -Raw
$manifest.Replace('BiliCopilot.Integration2743.Controls','BiliCopilot.Resize5a5e.Owned') | Set-Content "$Destination/Package.appxmanifest"
Add-Type -AssemblyName System.Drawing
$bitmap=[Drawing.Bitmap]::new(1600,900)
$drawing=[Drawing.Graphics]::FromImage($bitmap)
try {
    $drawing.Clear([Drawing.Color]::CornflowerBlue)
    $drawing.FillRectangle([Drawing.Brushes]::Gold,400,225,800,450)
    $bitmap.Save("$Destination/Assets/Cover.png")
} finally {$drawing.Dispose();$bitmap.Dispose()}
python "$PSScriptRoot/instrument.py" $Destination
if($LASTEXITCODE -ne 0){throw 'Instrumentation failed'}
