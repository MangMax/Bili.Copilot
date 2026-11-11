#Requires -Version 5.1

[CmdletBinding()]
param(
    [switch]$CertificateOnly
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$powerShellExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

function Test-Administrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-CertificateTrusted {
    param([string]$Thumbprint)

    $storeNames = @(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreName]::TrustedPeople
    )
    $location = [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine

    foreach ($storeName in $storeNames) {
        $store = New-Object -TypeName System.Security.Cryptography.X509Certificates.X509Store -ArgumentList $storeName, $location
        try {
            $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
            foreach ($candidate in $store.Certificates) {
                if ($candidate.Thumbprint -eq $Thumbprint) {
                    return $true
                }
            }
        } catch {
            Write-Verbose "无法读取证书存储 $storeName：$($_.Exception.Message)"
        } finally {
            $store.Close()
        }
    }

    return $false
}

function Install-TrustedCertificate {
    param([System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)

    $storeName = [System.Security.Cryptography.X509Certificates.StoreName]::Root
    $location = [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine
    $store = New-Object -TypeName System.Security.Cryptography.X509Certificates.X509Store -ArgumentList $storeName, $location
    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $store.Add($Certificate)
    } finally {
        $store.Close()
    }
}

function Wait-ForExit {
    if ($env:BILICOPILOT_FROM_INSTALL_CMD -eq '1') {
        return
    }
    if ($Host.Name -eq 'ConsoleHost' -and -not [Console]::IsInputRedirected) {
        Write-Host ''
        Read-Host '按 Enter 键退出' | Out-Null
    }
}

try {
    Set-Location -LiteralPath $scriptDir

    $certFile = Get-ChildItem -LiteralPath $scriptDir -File -Filter '*.cer' -ErrorAction SilentlyContinue |
        Select-Object -First 1
    $certificate = if ($certFile) {
        New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certFile.FullName)
    } else {
        $null
    }

    if ($CertificateOnly) {
        if (-not $certificate) {
            throw '未找到证书文件（*.cer）。'
        }
        if (-not (Test-Administrator)) {
            throw '导入证书需要管理员权限。'
        }
        if (-not (Test-CertificateTrusted $certificate.Thumbprint)) {
            Install-TrustedCertificate $certificate
        }
        exit 0
    }

    Write-Host '=== Bili Copilot 安装程序 ===' -ForegroundColor Cyan

    if (-not $certificate) {
        Write-Warning '未找到证书文件（*.cer），跳过证书导入。'
    } elseif (Test-CertificateTrusted $certificate.Thumbprint) {
        Write-Host '证书已受信任，跳过导入。' -ForegroundColor Gray
    } else {
        Write-Host "正在导入证书：$($certFile.Name)"
        if (Test-Administrator) {
            Install-TrustedCertificate $certificate
        } else {
            Write-Host '导入证书需要管理员权限，请在弹窗中确认。' -ForegroundColor Yellow
            $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -CertificateOnly"
            try {
                $process = Start-Process -FilePath $powerShellExe -ArgumentList $arguments -Verb RunAs -Wait -PassThru
            } catch {
                throw "无法获取管理员权限。请右键以管理员身份运行 Install.cmd 后重试。($($_.Exception.Message))"
            }
            if ($process.ExitCode -ne 0 -or -not (Test-CertificateTrusted $certificate.Thumbprint)) {
                throw '证书导入失败。请右键以管理员身份运行 Install.cmd 后重试。'
            }
        }
        Write-Host '证书已导入。' -ForegroundColor Green
    }

    $packages = @(Get-ChildItem -LiteralPath $scriptDir -File |
        Where-Object { $_.Extension -in '.msix', '.appx' })
    $appPackage = $packages |
        Where-Object { $_.Name -notlike 'Microsoft.*' } |
        Select-Object -First 1
    $dependencies = @($packages | Where-Object { $_.Name -like 'Microsoft.*' })

    if (-not $appPackage) {
        throw '未找到主程序安装包（*.msix）。请确认压缩包已完整解压。'
    }

    $installParameters = @{
        Path                     = $appPackage.FullName
        ForceApplicationShutdown = $true
        ErrorAction              = 'Stop'
    }
    if ($dependencies.Count -gt 0) {
        Write-Host "正在准备 $($dependencies.Count) 个运行库依赖..."
        $installParameters['DependencyPath'] = @($dependencies | ForEach-Object { $_.FullName })
    }

    Write-Host "正在安装：$($appPackage.Name)"
    Write-Host '请稍候，安装过程可能需要几分钟...' -ForegroundColor Cyan
    Add-AppxPackage @installParameters

    Write-Host ''
    Write-Host '安装完成！你可以在开始菜单中找到“哔哩助理”。' -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ''
    Write-Host '安装失败：' -ForegroundColor Red
    Write-Host $_.Exception.Message
    if ($_.Exception.Message -match '0x80073CF3') {
        Write-Host '提示：缺少应用运行库依赖，请确认压缩包内有 Microsoft.WindowsAppRuntime.*.msix。' -ForegroundColor Yellow
    }
    Wait-ForExit
    exit 1
}
