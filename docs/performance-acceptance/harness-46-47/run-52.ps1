param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$Layout,[Parameter(Mandatory)][string]$Evidence,[switch]$SkipLegacy,[int]$CasesCount=17,[switch]$Boundary,[switch]$Slots,[switch]$CaptureScreen)
$ErrorActionPreference='Stop'
$env:WINAPP_UI_WORKFLOW_ID='acceptance-52-84527'
$checks=[Collections.Generic.List[object]]::new()
$captures=[Collections.Generic.List[string]]::new()
function Ui([string[]]$Arguments) {
    $output=& winapp ui @Arguments -a $AppPid --json
    if($LASTEXITCODE -ne 0){throw "winapp ui $Arguments failed: $output"}
    $output
}
function Capture([string]$Name) {
    $path=Join-Path $Evidence "$Name.png"
    $captureArgs=@('screenshot','-w',$script:ownedHwnd,'--output',$path)
    if($CaptureScreen){Ui @('focus','Next52') | Out-Null; $captureArgs+='--capture-screen'}
    Ui $captureArgs | Out-Null
    if(-not(Test-Path $path)){throw 'Screenshot missing'}
    $captures.Add($path)
    Ui @('inspect','--depth','10') | Set-Content "$Evidence/$Name.uia.json"
}
function Await([string]$Name,[switch]$Terminal) {
    $expected=if($Terminal){"${Name}: terminal"}else{"${Name}:"}; Ui @('wait-for','HarnessStatus','--value',$expected,'--contains','-t','20000') | Out-Null
    $state=Ui @('get-value','HarnessStatus') | ConvertFrom-Json
    Capture $Name
    $checks.Add(@{name=$Name;passed=($state.text -like "${Name}: PASS*" -or $state.text -like "${Name}: terminal PASS*");status=$state.text})
}
try {
    $p=Get-Process -Id $AppPid
    if($AppPid -eq 9608 -or -not $p.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Owned process guard failed'}
    Ui @('wait-for','HarnessStatus','-t','15000') | Out-Null
    $windows=Ui @('list-windows') | ConvertFrom-Json
    $script:ownedHwnd=[string]($windows | Where-Object title -eq 'Offline acceptance 46 47 — eeb28f2' | Select-Object -First 1).hwnd
    if(-not $script:ownedHwnd){throw 'Owned window missing'}
    if(-not $SkipLegacy) {
    Ui @('invoke','Initial') | Out-Null; Await InitialTemplateLoadedReady
    Ui @('invoke','Detach') | Out-Null; Await Detach
    Ui @('invoke','Attach') | Out-Null; Await SameInstanceUnchangedTextAttachReady
    Ui @('invoke','Lifecycle') | Out-Null
    foreach($case in @('RepeatedAttach1Ready','RepeatedAttach2Ready','RepeatedAttach3Ready','EmptyTextReattach')) { Await $case; Ui @('invoke','Continue') | Out-Null }
    Await LifecycleBoundariesReady
    }
    foreach($case in (@('Landscape','Portrait','Square','Resize','SvgCharset','CachedProvider','LateProviderUnload','LateProviderClear','LateProviderReattach','LateProviderReopen','SlowBodyUnload','SlowBodyClear','SlowBodyReattach','SlowBodyReopen','ConcurrentHTTP','InlineHttpReattach','InlineLateProvider') | Select-Object -First $CasesCount)) {
        Ui @('invoke','Next52') | Out-Null
        Ui @('wait-for','Next52','-p','IsEnabled','--value','True','-t','20000') | Out-Null
        Await $case -Terminal
    }
    if($Slots) { Ui @('invoke','Slots52') | Out-Null; Ui @('wait-for','Slots52','-p','IsEnabled','--value','True','-t','20000') | Out-Null; Await Slots52 -Terminal }
    if($Boundary) { Ui @('invoke','Boundary52') | Out-Null; Ui @('wait-for','Boundary52','-p','IsEnabled','--value','True','-t','20000') | Out-Null; Await Boundary52 -Terminal }
} catch { $checks.Add(@{name='Runner';passed=$false;error="$_"}) }
finally {
    foreach($f in @('image52.json','observations.json','exceptions52.json','lifecycle52.json')) { if(Test-Path "$Layout/$f"){Copy-Item "$Layout/$f" "$Evidence/$f"} }
    try {
        $p=Get-Process -Id $AppPid -ErrorAction SilentlyContinue
        if($p -and $AppPid -ne 9608 -and $p.Path.StartsWith($Layout+'\',[StringComparison]::OrdinalIgnoreCase)) {
            Ui @('invoke','Close') | Out-Null
            if(-not $p.WaitForExit(15000)){throw 'Owned process failed to exit'}
        }
        $package=Get-AppxPackage -Name BiliCopilot.Acceptance52T384527
        if($package){ if($package.InstallLocation -ne $Layout){throw 'Package layout mismatch'}; Remove-AppxPackage -Package $package.PackageFullName }
        $checks.Add(@{name='Cleanup';passed=($null -eq (Get-Process -Id $AppPid -ErrorAction SilentlyContinue) -and $null -eq (Get-AppxPackage -Name BiliCopilot.Acceptance52T384527))})
    } catch { $checks.Add(@{name='Cleanup';passed=$false;error="$_"}) }
    & winapp ui yield --json | Set-Content "$Evidence/yield.json"
    @{checks=$checks.ToArray();captures=$captures.ToArray();appPid=$AppPid;layout=$Layout;target='local'} | ConvertTo-Json -Depth 8 | Set-Content "$Evidence/results.json"
}
$checks | ForEach-Object{[pscustomobject]$_} | Format-Table -AutoSize
if(@($checks | Where-Object {-not $_.passed}).Count){exit 1}
