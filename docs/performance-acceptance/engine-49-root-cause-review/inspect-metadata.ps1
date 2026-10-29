param([string]$ArtifactRoot='C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-c9d5219a/artifacts/engine49')
$ErrorActionPreference='Stop'
$result=@()
foreach($engine in @('Core','Legacy')) {
 $path=Join-Path $ArtifactRoot "layout-final/Danmaku.$engine.dll"
 $stream=[IO.File]::OpenRead($path)
 $pe=[System.Reflection.PortableExecutable.PEReader]::new($stream)
 try {
  $md=[System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
  foreach($th in $md.TypeDefinitions) {
   $td=$md.GetTypeDefinition($th); $type=$md.GetString($td.Name)
   if($type -in @('DanmakuScheduler','DanmakuFrostMaster','DanmakuRender')) {
    $fields=@($td.GetFields() | ForEach-Object {$md.GetString($md.GetFieldDefinition($_).Name)})
    $methods=@()
    if($type -eq 'DanmakuRender') {
     foreach($mh in $td.GetMethods()) {
      $m=$md.GetMethodDefinition($mh);$name=$md.GetString($m.Name)
      if($name -in @('Pause','OnCanvasUpdate')) {
       $body=[System.Reflection.Metadata.PEReaderExtensions]::GetMethodBody($pe,$m.RelativeVirtualAddress)
       $methods+=@{name=$name;il=[Convert]::ToHexString($body.GetILBytes())}
      }
     }
    }
    $result+=@{path=$path;sha256=(Get-FileHash $path -Algorithm SHA256).Hash;type=$type;fields=$fields;methods=$methods}
   }
  }
 } finally {$pe.Dispose();$stream.Dispose()}
}
$result | ConvertTo-Json -Depth 8
