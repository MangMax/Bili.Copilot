param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference='Stop'
$text=Get-Content $Path -Raw
if($text.Contains('function Read-FixtureJson')){return}
$helper=@'
function Read-FixtureJson([string]$Path) {
 try { return Get-Content $Path -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop }
 catch {
  Add-Content "$Evidence/json-read-retries.log" "$([DateTime]::UtcNow.ToString('o')) $Path : $($_.Exception.Message)"
  return $null
 }
}
'@
$text=$text.Replace('$captures=[Collections.Generic.List[object]]::new()', $helper+"`n"+'$captures=[Collections.Generic.List[object]]::new()')
$text=$text.Replace('if(Test-Path "$state/results.json"){break}', 'if(Test-Path "$state/results.json"){$finished=Read-FixtureJson "$state/results.json";if(@($finished).Count -eq 15){break}}')
$text=$text.Replace('$capture=Get-Content "$state/capture.json" -Raw | ConvertFrom-Json', '$capture=Read-FixtureJson "$state/capture.json";if(!$capture.name -or !$capture.hostHwnd){Start-Sleep -Milliseconds 100;continue}')
Set-Content $Path $text
