param([string]$Runs='artifacts/recycled-cards')
$ErrorActionPreference='Stop'
$receipts=@(Get-ChildItem $Runs -Directory | Where-Object {(Test-Path "$($_.FullName)/identity.json") -and (Test-Path "$($_.FullName)/exit.json")})
$start=($receipts | ForEach-Object {[datetime](Get-Content "$($_.FullName)/identity.json" -Raw | ConvertFrom-Json).startedUtc} | Sort-Object | Select-Object -First 1)
$events=@(Get-WinEvent -FilterHashtable @{LogName='Application';Id=1000,1001;StartTime=$start} -ErrorAction SilentlyContinue)
foreach($stage in $receipts) {
    $identity=Get-Content "$($stage.FullName)/identity.json" -Raw | ConvertFrom-Json
    $exitReceipt=Get-Content "$($stage.FullName)/exit.json" -Raw | ConvertFrom-Json
    $from=[datetime]$identity.startedUtc
    $observed=if($exitReceipt.closeUtc){[datetime]$exitReceipt.closeUtc}else{[datetime]$exitReceipt.observationStart}
    $until=$observed.AddSeconds($exitReceipt.observedSeconds)
    $matches=@(foreach($event in $events) {
        if($event.TimeCreated -lt $from -or $event.TimeCreated -gt $until){continue}
        $pathMatch=$event.Message.IndexOf($identity.path,[StringComparison]::OrdinalIgnoreCase) -ge 0
        $eventPid=$null
        $xml=[xml]$event.ToXml()
        foreach($data in $xml.Event.EventData.Data) {
            if($data.Name -eq 'ProcessId') {
                $value=[string]$data.'#text'
                try {$eventPid=if($value.StartsWith('0x')){[Convert]::ToInt64($value.Substring(2),16)}else{[long]$value}}catch{}
            }
        }
        $pidMatch=($null -ne $eventPid -and $eventPid -eq $identity.pid -and $event.Message.Contains('Harness.exe'))
        if($pathMatch -or $pidMatch){@{time=$event.TimeCreated.ToUniversalTime().ToString('O');id=$event.Id;message=$event.Message;exactPathMatch=$pathMatch;eventPid=$eventPid;exactPidMatch=$pidMatch}}
    })
    @{pid=$identity.pid;path=$identity.path;fromUtc=$from.ToUniversalTime().ToString('O');untilUtc=$until.ToUniversalTime().ToString('O');observedSeconds=$exitReceipt.observedSeconds;events=$matches} | ConvertTo-Json -Depth 6 | Set-Content "$($stage.FullName)/wer-exact.json" -Encoding UTF8
}
Write-Output "Exact PID/path WER receipts: $($receipts.Count)"
