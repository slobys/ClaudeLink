[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$dotnet=Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
& $dotnet build (Join-Path $projectRoot 'src/Launcher.App') -c Release --nologo -v minimal
if($LASTEXITCODE -ne 0){throw 'Brand renderer build failed.'}
$renderer=Join-Path $projectRoot 'src/Launcher.App/bin/Release/net10.0-windows/ClaudeNetworkLauncher.dll'
$target=Join-Path $projectRoot 'src/Launcher.App/Assets'
$process=Start-Process -FilePath $dotnet -ArgumentList @(('"'+$renderer+'"'),'--export-brand',('"'+$target+'"')) -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(60000)){throw 'Brand rendering timed out.'}
if($process.ExitCode -ne 0){throw 'Brand rendering failed.'}
Write-Output 'Vector brand rendered to 1024px PNG and 7 native ICO sizes.'
