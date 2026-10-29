param([Parameter(Mandatory)][string]$Root)
$ErrorActionPreference = 'Stop'
$result = foreach ($path in Get-ChildItem $Root -Recurse -Filter 'Danmaku.*.dll' | Where-Object { $_.FullName -match '\\bin\\x64\\Release\\' -and $_.FullName -notmatch '\\ref\\' }) {
    $stream = [IO.File]::OpenRead($path.FullName)
    $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $fields = @($md.TypeDefinitions | ForEach-Object {
            $type = $md.GetTypeDefinition($_)
            $name = $md.GetString($type.Name)
            foreach ($handle in $type.GetFields()) {
                $field = $md.GetString($md.GetFieldDefinition($handle).Name)
                if ($field -match '^_test|trace49|Trace49') { "$name.$field" }
            }
        })
        [ordered]@{path=$path.FullName; sha256=(Get-FileHash $path.FullName).Hash; diagnosticFields=$fields; mvid=$md.GetGuid($md.GetModuleDefinition().Mvid).ToString()}
    } finally { $pe.Dispose(); $stream.Dispose() }
}
$result | ConvertTo-Json -Depth 6
