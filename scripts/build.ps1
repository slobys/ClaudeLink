[CmdletBinding()]
param([switch]$SkipBootstrap)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
if(-not $SkipBootstrap){ & (Join-Path $PSScriptRoot 'bootstrap.ps1') }
& (Join-Path $PSScriptRoot 'sync-theme.ps1')
$dotnet=Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$artifacts=Join-Path $projectRoot 'artifacts'
$publish=Join-Path $artifacts 'publish'
$payload=Join-Path $artifacts 'payload'
New-Item -ItemType Directory -Force -Path $artifacts,$payload | Out-Null
& $dotnet publish '.\src\Launcher.App\Launcher.App.csproj' -c Release -r win-x64 --self-contained true -o $publish --nologo -v minimal
if($LASTEXITCODE -ne 0){throw 'Publish failed.'}
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler=Join-Path $framework 'csc.exe'
$references=@('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Core.dll','/r:Microsoft.CSharp.dll','/r:System.Web.Extensions.dll',('/r:'+(Join-Path $framework 'System.IO.Compression.dll')),('/r:'+(Join-Path $framework 'System.IO.Compression.FileSystem.dll')))
$common=@('/nologo','/target:winexe','/platform:anycpu','/optimize+','/langversion:5','/codepage:65001',('/win32icon:'+(Join-Path $projectRoot 'src\Launcher.App\Assets\App.ico')),('/win32manifest:'+(Join-Path $projectRoot 'src\Launcher.App\app.manifest')))
& $compiler @common @references '/define:UNINSTALLER' ('/out:'+(Join-Path $payload 'Uninstall.exe')) (Join-Path $projectRoot 'installer\Installer.cs')
if($LASTEXITCODE -ne 0){throw 'Uninstaller compilation failed.'}
Copy-Item -LiteralPath (Join-Path $publish 'ClaudeNetworkLauncher.exe') -Destination (Join-Path $payload 'ClaudeNetworkLauncher.exe') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\使用指南.md') -Destination (Join-Path $payload '使用指南.md') -Force
$licenses=Join-Path $payload 'Licenses'
New-Item -ItemType Directory -Force -Path $licenses | Out-Null
foreach($name in @('sing-box-LICENSE.txt','Twemoji-LICENSE.txt','NaiLink-LICENSE.txt','GPL-3.0.txt','YamlDotNet-LICENSE.txt','core-manifest.json','dotnet-LICENSE.txt','dotnet-ThirdPartyNotices.txt')){
    Copy-Item -LiteralPath (Join-Path $projectRoot ('vendor\'+$name)) -Destination (Join-Path $licenses $name) -Force
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\THIRD-PARTY-NOTICES.md') -Destination (Join-Path $licenses 'THIRD-PARTY-NOTICES.md') -Force
$fileManifest=[ordered]@{}
foreach($file in Get-ChildItem -LiteralPath $payload -File -Recurse){
    $relative=[IO.Path]::GetRelativePath($payload,$file.FullName).Replace('\','/')
    $fileManifest[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$manifestPath=Join-Path $artifacts 'payload-manifest.json'
$fileManifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payloadArchive=Join-Path $artifacts 'payload.zip'
if(Test-Path -LiteralPath $payloadArchive){Remove-Item -LiteralPath $payloadArchive}
[IO.Compression.ZipFile]::CreateFromDirectory($payload,$payloadArchive,[IO.Compression.CompressionLevel]::Optimal,$false)
$setup=Join-Path $artifacts 'ClaudeLink-Setup-1.4.1.exe'
& $compiler @common @references ('/out:'+$setup) ('/resource:'+$payloadArchive+',Payload.zip') ('/resource:'+$manifestPath+',Payload.manifest.json') (Join-Path $projectRoot 'installer\Installer.cs')
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed.'}
Copy-Item -LiteralPath $payloadArchive -Destination (Join-Path $artifacts 'ClaudeLink-Portable-1.4.1-win-x64.zip') -Force
$hashes=@{}
foreach($path in @($setup,(Join-Path $artifacts 'ClaudeLink-Portable-1.4.1-win-x64.zip'),(Join-Path $publish 'ClaudeNetworkLauncher.exe'))){
    $hashes[[IO.Path]::GetFileName($path)]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifacts 'SHA256.json') -Encoding UTF8
Get-Item -LiteralPath $setup,(Join-Path $publish 'ClaudeNetworkLauncher.exe') | Select-Object Name,Length,FullName | ConvertTo-Json
