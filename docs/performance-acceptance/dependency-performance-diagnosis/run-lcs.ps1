param([Parameter(Mandatory)][string]$Executable,[Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
if(Test-Path $Evidence){throw 'Fresh evidence directory required'}
New-Item -ItemType Directory $Evidence | Out-Null
$order=@()
$index=0
foreach($variant in @('Rectangular','Jagged','Shortcut','Shortcut','Jagged','Rectangular')){
    $index++
    $start=[DateTime]::UtcNow.ToString('o')
    & $Executable $variant "$Evidence/$index-$variant.json"
    if($LASTEXITCODE -ne 0){throw 'Behavior or measurement failed'}
    $order+=@{index=$index;variant=$variant;startUtc=$start;endUtc=[DateTime]::UtcNow.ToString('o')}
    $order | ConvertTo-Json | Set-Content "$Evidence/order.json"
}
