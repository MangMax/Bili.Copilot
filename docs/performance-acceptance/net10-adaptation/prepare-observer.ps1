param([Parameter(Mandatory)][string]$Fixture)
$ErrorActionPreference='Stop'
$file=Join-Path $Fixture 'IdleBaseline.cs'
$source=Get-Content $file -Raw
$start=$source.IndexOf('            for (var i = 0; i <= 60; i++)')
$end=$source.IndexOf('        for (var i = 0; i < 20; i++)')
if($start -lt 0 -or $end -lt $start){throw 'Unexpected legacy fixture'}
$replacement=@'
            // Protocol v2: only phase boundaries execute on the dispatcher.
            // No Process.Refresh, counter read, UI update or per-sample timer.
            var marker = Path.Combine(Offline48.Folder, $"observe-{phase}.json");
            var temp = marker + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new {phase, pid=Environment.ProcessId,
                visible=main.AppWindow.IsVisible, hwnd=hwnd.ToInt64(), scale=root.XamlRoot.RasterizationScale}));
            File.Move(temp, marker);
            await Task.Delay(63000);
            if (!File.Exists(Path.Combine(Offline48.Folder, $"observe-{phase}-done.json")))
                throw new Exception("External observer did not finish phase " + phase);
        }

'@
$source=$source.Substring(0,$start)+$replacement+$source.Substring($end)
$source=$source.Replace('        var rows = new List<object>();','').Replace('        using var process = Process.GetCurrentProcess();','')
Set-Content $file $source
