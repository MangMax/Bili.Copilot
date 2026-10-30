param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$Destination=[IO.Path]::GetFullPath($Destination)
if(Test-Path $Destination){throw 'Fresh destination required'}
New-Item -ItemType Directory $Destination | Out-Null
foreach($arm in 'player10final','player10common','player9common'){
 $fixture="$Destination/$arm"
 $runId=switch($arm){player10final{'N854F'} player10common{'N854C'} player9common{'N854B'}}
 & "$PSScriptRoot/../dependency-upgrade/prepare.ps1" -Destination $fixture -RunId $runId
 & "$PSScriptRoot/prepare-observer.ps1" -Fixture $fixture
 if($arm -ne 'player10final'){
  $p="$fixture/Directory.Packages.props"
  (Get-Content $p -Raw).Replace('Include="H.NotifyIcon.WinUI" Version="2.4.1"','Include="H.NotifyIcon.WinUI" Version="2.3.2"') | Set-Content $p
 }
 if($arm -eq 'player9common'){
  $name='BiliCopilot.Visor.Models'
  foreach($file in git -C $root ls-files "src/Desktop/$name"){
   $target=Join-Path "$Destination/refs9" $file.Substring('src/Desktop/'.Length)
   New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
   Copy-Item "$root/$file" $target
  }
  $p="$Destination/refs9/$name/$name.csproj"
  (Get-Content $p -Raw).Replace('<TargetFramework>net10.0</TargetFramework>','<TargetFramework>net9.0</TargetFramework>') | Set-Content $p
  foreach($file in 'Directory.Packages.props','Directory.Build.props','.editorconfig'){Copy-Item "$root/src/$file" "$Destination/refs9/$file"}
  $p="$fixture/BiliCopilot.UI.csproj"
  $s=(Get-Content $p -Raw).Replace('<TargetFramework>net10.0-windows','<TargetFramework>net9.0-windows')
  $s=$s.Replace("$root\src\Desktop\$name\$name.csproj",[IO.Path]::GetFullPath("$Destination/refs9/$name/$name.csproj"))
  $s=$s.Replace('<PackageReference Include="System.Management" />','<PackageReference Include="System.Management" />'+"`n    <PackageReference Include=`"System.Reflection.Metadata`" />")
  Set-Content $p $s
 }
 & dotnet build "$fixture/BiliCopilot.UI.csproj" -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false "-p:SolutionDir=$root/src/" *> "$Destination/$arm-build.log"
 if($LASTEXITCODE -ne 0){throw "$arm build failed"}
 $outputs=@(Get-ChildItem "$fixture/bin" -Recurse -Filter AppxManifest.xml | Where-Object FullName -Like '*\Release\*')
 if($outputs.Count -ne 1){throw 'Build output ambiguity'}
 & "$PSScriptRoot/freeze.ps1" -Fixture $fixture -BuildOutput $outputs[0].Directory.FullName
}
