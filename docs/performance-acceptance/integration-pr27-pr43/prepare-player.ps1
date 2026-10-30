param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
& "$PSScriptRoot/../harness-48-duration-fix/prepare.ps1" -SourceRoot $root -Destination $Destination
if($LASTEXITCODE -and $LASTEXITCODE -ne 0){throw 'Preparation failed'}
$app=Get-Content "$root/src/Desktop/BiliCopilot.UI/App.xaml.cs" -Raw
$app=$app.Replace('Richasy.BiliCopilot"','Integration2743.Offline"').Replace('GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().LaunchCommand.Execute(default);','InitializeTrayLifecycle(); Offline48.Start();')
$app | Set-Content "$Destination/App.xaml.cs"
$manifest=Get-Content "$Destination/Package.Dev.appxmanifest" -Raw
$manifest.Replace('Mang.BiliCopilot.Offline48.D1d19DurationFix','Mang.BiliCopilot.Integration2743.Player') | Set-Content "$Destination/Package.Dev.appxmanifest"
Copy-Item "$Destination/Package.Dev.appxmanifest" "$Destination/Package.appxmanifest" -Force
$fixture=Get-Content "$Destination/Fixture.cs" -Raw
$fixture=$fixture.Replace('vm.Close();','await vm.CloseAsync();')
$fixture=$fixture.Replace('static async Task<PlayerViewModel> Open(', 'internal static async Task<PlayerViewModel> Open(')
$fixture=$fixture.Replace('Settings["PlayerVolume"]=0d;', 'Settings["PlayerVolume"]=0d; Settings["IsNotificationEnabled"]=false;')
$fixture=$fixture.Replace('Host.Content=new StackPanel', 'var mainRun=new Button {Content="Main lifetime checks"};AutomationProperties.SetAutomationId(mainRun,"MainChecks");mainRun.Click+=async (_,_)=>await MainRuntimeChecks.Run(); Host.Content=new StackPanel')
$fixture=$fixture.Replace('Status,run,next}', 'Status,run,next,mainRun}')
Copy-Item "$PSScriptRoot/MainRuntimeChecks.cs" "$Destination/MainRuntimeChecks.cs"
$fixture=$fixture.Replace('Check(Errors.Count==0,"unexpected boundary error");','Check(!Errors.Any(e=>!e.StartsWith("Failed to load image from ms-appx:")),"unexpected boundary error: "+string.Join(" | ",Errors));')
$fixture=$fixture.Replace('await Capture("results");',@'
await Case("RealTrayEnableDisableRecreate",async()=>{
    Errors.Clear();
    var app=(App)Application.Current;
    Check(!app.TrayLifecycle!.HandleClose(),"Default must not intercept close");
    for(var i=0;i<3;i++) {
        app.SetTrayEnabled(true);
        Check(app.TrayLifecycle.HandleClose(),"Real H.NotifyIcon was not created");
        Check(app.TrayLifecycle.IsResident,"Residency missing");
        app.SetTrayEnabled(false);
        Check(!app.TrayLifecycle.IsResident && !app.TrayLifecycle.HandleClose(),"Disable did not release residency");
    }
    await Drain();
    Check(!Errors.Any(e=>e.Contains("TrayIcon is disposed") || e.Contains("Unhandled exception")),"Late tray callback: "+string.Join(" | ",Errors));
});
await Capture("results");
'@)
$fixture | Set-Content "$Destination/Fixture.cs"
$runner=Get-Content "$PSScriptRoot/../harness-48-duration-fix/run-ui.ps1" -Raw
$runner.Replace('offline48-durationfix-1d19','integration2743').Replace('Mang.BiliCopilot.Offline48.D1d19DurationFix','Mang.BiliCopilot.Integration2743.Player').Replace('Count -ne 12','Count -ne 13').Replace('Expected twelve','Expected thirteen') | Set-Content "$Destination/run-ui.ps1"
