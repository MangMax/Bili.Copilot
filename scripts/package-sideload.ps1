[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceDir,
    [Parameter(Mandatory)][string]$Platform,
    [Parameter(Mandatory)][string]$Version,
    [string]$RepositoryRoot,
    [string]$RuntimeVersion
)

$ErrorActionPreference = 'Stop'

if (-not $RepositoryRoot) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$SourceDir = (Resolve-Path -LiteralPath $SourceDir).Path

$architecture = switch ($Platform.ToLowerInvariant()) {
    'x64' { 'win10-x64' }
    'arm64' { 'win10-arm64' }
    default { throw "不支持的平台：$Platform" }
}

if (-not $RuntimeVersion) {
    $propsPath = Join-Path $RepositoryRoot 'src\Directory.Packages.props'
    $match = Select-String -Path $propsPath -Pattern 'Include="Microsoft\.WindowsAppSDK"\s+Version="([^"]+)"' |
        Select-Object -First 1
    if (-not $match) {
        throw "无法从 $propsPath 读取 Microsoft.WindowsAppSDK 版本。"
    }
    $RuntimeVersion = $match.Matches[0].Groups[1].Value
}

$globalPackages = $env:NUGET_PACKAGES
if (-not $globalPackages) {
    $globalPackages = (& dotnet nuget locals global-packages --list |
        Where-Object { $_ -match 'global-packages:' } |
        Select-Object -First 1) -replace '^.*global-packages:\s*', ''
}
if (-not $globalPackages) {
    $globalPackages = Join-Path $env:USERPROFILE '.nuget\packages'
}

$runtimePackage = Join-Path $globalPackages "microsoft.windowsappsdk.runtime\$RuntimeVersion\tools\MSIX\$architecture\Microsoft.WindowsAppRuntime.2.msix"
if (-not (Test-Path -LiteralPath $runtimePackage)) {
    throw "未找到 Windows App Runtime 依赖包：$runtimePackage。请先执行 NuGet 还原。"
}

# The build output may contain extra Microsoft.* packages. Ship exactly the
# framework package the app declares, otherwise Add-AppxPackage rejects
# dependency paths the app does not reference.
Get-ChildItem -LiteralPath $SourceDir -File |
    Where-Object { $_.Name -like 'Microsoft.*' -and $_.Extension -in '.msix', '.appx' } |
    Remove-Item -Force

Copy-Item -LiteralPath $runtimePackage -Destination $SourceDir -Force
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'scripts\Install.ps1') -Destination $SourceDir -Force
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'scripts\Install.cmd') -Destination $SourceDir -Force
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'docs\sideload-guide.txt') -Destination (Join-Path $SourceDir '安装说明.txt') -Force

if (-not (Get-ChildItem -LiteralPath $SourceDir -File -Filter '*.cer' -ErrorAction SilentlyContinue)) {
    $releaseCertificate = Join-Path $RepositoryRoot 'signing.cer'
    if (Test-Path -LiteralPath $releaseCertificate) {
        Copy-Item -LiteralPath $releaseCertificate -Destination $SourceDir -Force
    }
}

$appPackage = Get-ChildItem -LiteralPath $SourceDir -File |
    Where-Object { $_.Extension -in '.msix', '.appx' -and $_.Name -notlike 'Microsoft.*' } |
    Select-Object -First 1
if (-not $appPackage) {
    throw "在 $SourceDir 中未找到主程序安装包（*.msix）。"
}
if (-not (Get-ChildItem -LiteralPath $SourceDir -File -Filter '*.cer' -ErrorAction SilentlyContinue)) {
    throw "在 $SourceDir 中未找到证书文件（*.cer）。"
}

$zipName = "BiliCopilot_${Version}_${Platform}.zip"
$zipPath = Join-Path $SourceDir $zipName
$files = @(Get-ChildItem -LiteralPath $SourceDir -File | Where-Object { $_.Extension -ne '.zip' })
if ($files.Count -eq 0) {
    throw "没有可打包的文件：$SourceDir"
}

Compress-Archive -Path $files.FullName -DestinationPath $zipPath -Force
Get-ChildItem -LiteralPath $SourceDir -File | Where-Object { $_.Name -ne $zipName } | Remove-Item -Force
Write-Output $zipPath
