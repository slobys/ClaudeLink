[CmdletBinding()]
param([switch]$SkipUi,[switch]$SkipInstaller)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$dotnet=Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$artifacts=Join-Path $projectRoot 'artifacts'
$configs=Join-Path $artifacts 'test-configs'
& $dotnet run --project '.\tests\Launcher.Tests\Launcher.Tests.csproj' -- $configs
if($LASTEXITCODE -ne 0){throw 'Core regression tests failed.'}
$checked=0
foreach($config in Get-ChildItem -LiteralPath $configs -Filter '*.json' | Where-Object Name -ne 'results.json'){
    & '.\vendor\sing-box.exe' check -c $config.FullName
    if($LASTEXITCODE -ne 0){throw ('Official core config check failed: '+$config.Name)}
    $checked++
}
$app=Join-Path $artifacts 'publish\ClaudeNetworkLauncher.exe'
$integration=Join-Path $artifacts 'integration-results.json'
$process=Start-Process -FilePath $app -ArgumentList @('--integration-test',('"'+$integration+'"')) -WindowStyle Hidden -PassThru
# Includes six native browser launches plus protocol and process lifecycle fixtures on hosted runners.
$integrationDeadline=(Get-Date).AddMinutes(5)
while(-not $process.WaitForExit(500)){
    if((Get-Date) -gt $integrationDeadline){
        Stop-Process -Id $process.Id
        $lastCheck=if(Test-Path -LiteralPath $integration){(Get-Content -LiteralPath $integration -Raw | ConvertFrom-Json).Checks | Select-Object -Last 1}else{'No progress report'}
        throw ('Integration test timed out after: '+$lastCheck)
    }
}
$report=Get-Content -LiteralPath $integration -Raw | ConvertFrom-Json
if($process.ExitCode -ne 0 -or $report.Failed -or $report.InProgress){throw ('Integration test failed: '+$report.Failed)}
if(-not $SkipUi){
    $ui=Join-Path $artifacts 'ui-check'
    $process=Start-Process -FilePath $app -ArgumentList @('--smoke-test',('"'+$ui+'"')) -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(20000)){Stop-Process -Id $process.Id;throw 'UI smoke timed out.'}
    if($process.ExitCode -ne 0 -or @(Get-ChildItem -LiteralPath $ui -Filter '*.png').Count -lt 7){throw 'UI renders failed.'}
}
$installerChecks=@()
if(-not $SkipInstaller){
    $testGuid=[guid]::NewGuid().ToString('N')
    $testRoot=Join-Path ([IO.Path]::GetTempPath()) ('ClaudeLink-SetupTest-'+$testGuid)
    $testRoot=[IO.Path]::GetFullPath($testRoot).TrimEnd('\')
    $expectedParent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if([IO.Path]::GetDirectoryName($testRoot) -ne $expectedParent){throw 'Test root containment check failed.'}
    $setup=Join-Path $artifacts 'ClaudeLink-Setup-1.3.2.exe'
    $registryPath='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClaudeLink.Test.'+$testGuid
    $env:CLAUDELINK_SETUP_TEST_ROOT=$testRoot
    try{
        $process=Start-Process -FilePath $setup -ArgumentList '/silent' -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(50000)){Stop-Process -Id $process.Id;throw 'Setup test timeout.'}
        if($process.ExitCode -ne 0){throw 'Isolated installation failed.'}
        $installedApp=Join-Path $testRoot 'ClaudeNetworkLauncher.exe'
        if((Get-FileHash -LiteralPath $installedApp).Hash -ne (Get-FileHash -LiteralPath $app).Hash){throw 'Installed app hash mismatch.'}
        if(-not (Test-Path -LiteralPath $registryPath)){throw 'Uninstall registration missing.'}
        $installerChecks+='Current-user installation and file hash verified'
        $installerChecks+='Isolated uninstall registration verified'
        Set-Content -LiteralPath (Join-Path $testRoot 'user-added-test.txt') -Value 'preserve-test'
        # Simulate an old launcher/shortcut inheriting the installation directory.
        $process=Start-Process -FilePath $setup -WorkingDirectory $testRoot -ArgumentList '/silent' -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(50000)){Stop-Process -Id $process.Id;throw 'Upgrade test timeout.'}
        if($process.ExitCode -ne 0){throw 'Upgrade test failed.'}
        $backup=@(Get-ChildItem -LiteralPath $expectedParent -Directory -Filter (('ClaudeLink-SetupTest-'+$testGuid)+'.previous-*'))
        if($backup.Count -ne 1 -or -not(Test-Path -LiteralPath (Join-Path $backup[0].FullName 'user-added-test.txt'))){throw 'Unknown file preservation failed.'}
        $installerChecks+='Upgrade preserves unknown files in previous installation backup'
        $installerChecks+='Upgrade launched inside the installation directory releases its own directory lock'
        $installedUi=Join-Path $artifacts 'installed-ui-check'
        $process=Start-Process -FilePath $installedApp -ArgumentList @('--smoke-test',('"'+$installedUi+'"')) -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(20000)){Stop-Process -Id $process.Id;throw 'Installed app launch timeout.'}
        if($process.ExitCode -ne 0){throw 'Installed single EXE launch failed.'}
        $installerChecks+='Installed self-contained EXE launches without adjacent .NET files'
        $waitHelper=Start-Process -FilePath (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -ArgumentList @('-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 3') -WindowStyle Hidden -PassThru
        $waitStarted=$waitHelper.StartTime.ToUniversalTime().Ticks
        $updateWatch=[Diagnostics.Stopwatch]::StartNew()
        $process=Start-Process -FilePath $setup -WorkingDirectory $testRoot -ArgumentList @('/update',$waitHelper.Id,$waitStarted,'/restart','/silent') -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(50000)){throw 'Self-update installer timeout.'}
        if($process.ExitCode -ne 0 -or $updateWatch.ElapsedMilliseconds -lt 2000){throw 'Self-update did not wait for its owner.'}
        $restartProof=Join-Path ($testRoot+'.restart-proof') 'ui-results.json'
        $restartDeadline=(Get-Date).AddSeconds(20)
        while((Get-Date) -lt $restartDeadline -and -not(Test-Path -LiteralPath $restartProof)){Start-Sleep -Milliseconds 200}
        if(-not(Test-Path -LiteralPath $restartProof)){throw 'Updated app did not restart into isolated smoke verification.'}
        if((Get-FileHash -LiteralPath $installedApp).Hash -ne (Get-FileHash -LiteralPath $app).Hash){throw 'Updated installation hash differs.'}
        $restartProcess=Get-CimInstance Win32_Process -Filter "Name='ClaudeNetworkLauncher.exe'" | Where-Object {$_.ExecutablePath -eq $installedApp}
        foreach($ownedRestart in $restartProcess){$ownedProcess=Get-Process -Id $ownedRestart.ProcessId -ErrorAction SilentlyContinue;if($ownedProcess -and -not $ownedProcess.WaitForExit(10000)){throw 'Restarted smoke window did not exit.'}}
        $installerChecks+='Self-update waits for exact owner process, installs verified payload and restarts GUI successfully'
        Set-Content -LiteralPath (Join-Path $testRoot 'preserved-after-uninstall.txt') -Value 'preserve-test'
        $uninstaller=Join-Path $testRoot 'Uninstall.exe'
        $process=Start-Process -FilePath $uninstaller -ArgumentList @('/uninstall','/silent') -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(15000)){Stop-Process -Id $process.Id;throw 'Uninstaller test timeout.'}
        $until=(Get-Date).AddSeconds(20)
        while((Get-Date) -lt $until -and -not (Test-Path -LiteralPath ($testRoot+'.uninstalled'))){Start-Sleep -Milliseconds 200}
        if((Test-Path -LiteralPath $installedApp) -or (Test-Path -LiteralPath $registryPath) -or -not (Test-Path -LiteralPath ($testRoot+'.uninstalled'))){throw 'Uninstall did not finish.'}
        if(-not(Test-Path -LiteralPath (Join-Path $testRoot 'preserved-after-uninstall.txt'))){throw 'Uninstall deleted unknown file.'}
        $installerChecks+='Uninstall removes product files and registry while preserving unknown files'
        $installerChecks | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifacts 'installer-results.json') -Encoding UTF8
    }finally{
        Remove-Item Env:\CLAUDELINK_SETUP_TEST_ROOT -ErrorAction SilentlyContinue
        # Only remove exclusively generated test paths after resolving and checking absolute containment.
        $cleanupTargets=@($testRoot,($testRoot+'.restart-proof'))+@(Get-ChildItem -LiteralPath $expectedParent -Directory -Filter (('ClaudeLink-SetupTest-'+$testGuid)+'.previous-*') -ErrorAction SilentlyContinue | ForEach-Object FullName)
        foreach($target in $cleanupTargets){
            $resolved=[IO.Path]::GetFullPath($target).TrimEnd('\')
            if([IO.Path]::GetDirectoryName($resolved) -ne $expectedParent -or -not [IO.Path]::GetFileName($resolved).StartsWith('ClaudeLink-SetupTest-'+$testGuid)){throw 'Refusing test cleanup outside exact test scope.'}
            if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
        }
        if(Test-Path -LiteralPath ($testRoot+'.uninstalled')){Remove-Item -LiteralPath ($testRoot+'.uninstalled')}
    }
}
$uiAssertions=if($SkipUi){0}else{(Get-Content -LiteralPath (Join-Path $artifacts 'ui-check\ui-results.json') -Raw | ConvertFrom-Json).Passed}
$summary=[ordered]@{CoreAssertions=(Get-Content -LiteralPath (Join-Path $configs 'results.json') -Raw | ConvertFrom-Json).Passed;OfficialCoreConfigs=$checked;IntegrationAssertions=$report.Passed;UiAssertions=$uiAssertions;InstallerChecks=$installerChecks.Count;LiveTunTested=$false;RealSubscriptionTested=$false}
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifacts 'validation-summary.json') -Encoding UTF8
$summary | ConvertTo-Json
