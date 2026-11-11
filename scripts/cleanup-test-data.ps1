# Removes only disposable data identifiable as this project's synthetic integration fixtures.
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$testParent=Join-Path $env:LOCALAPPDATA 'ClaudeNetworkLauncher\tests'
$removed=0
$bytesFreed=0L
Add-Type -AssemblyName System.Security.Cryptography.ProtectedData
$nativeId=$null
$nativeRecord=Join-Path $projectRoot 'artifacts\native-ui-session.json'
if(Test-Path -LiteralPath $nativeRecord){$nativeId=(Get-Content -LiteralPath $nativeRecord -Raw | ConvertFrom-Json).TestId}
if(Test-Path -LiteralPath $testParent){
    $expectedParent=[IO.Path]::GetFullPath($testParent).TrimEnd('\')
    foreach($directory in Get-ChildItem -LiteralPath $expectedParent -Directory){
        $fixtureGuid=[guid]::Empty
        if(-not [guid]::TryParseExact($directory.Name,'N',[ref]$fixtureGuid)){continue}
        $absolute=[IO.Path]::GetFullPath($directory.FullName).TrimEnd('\')
        if([IO.Path]::GetDirectoryName($absolute) -ne $expectedParent){throw 'Test cleanup containment failure.'}
        if(($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){continue}
        $identified=$directory.Name -eq $nativeId
        $settings=Join-Path $absolute 'settings.dat'
        if(-not $identified -and (Test-Path -LiteralPath $settings)){
            try{
                $plain=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($settings),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
                $decoded=[Text.Encoding]::UTF8.GetString($plain) | ConvertFrom-Json
                $identified=@($decoded.Subscriptions).Count -eq 1 -and $decoded.Subscriptions[0].Url -ceq ('https://example.invalid/test-only-secret-'+$directory.Name)
                [Array]::Clear($plain,0,$plain.Length)
            }catch{$identified=$false}
        }
        if(-not $identified){continue}
        $active=$false
        $sessions=Join-Path $absolute 'sessions'
        if(Test-Path -LiteralPath $sessions){
            foreach($sessionDirectory in Get-ChildItem -LiteralPath $sessions -Directory){
                $statusPath=Join-Path $sessionDirectory.FullName 'status.json'
                if(-not(Test-Path -LiteralPath $statusPath)){continue}
                $status=Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
                foreach($pair in @(@($status.HostPid,$status.HostStarted),@($status.CorePid,$status.CoreStarted))){
                    if([int]$pair[0] -le 0){continue}
                    $process=Get-Process -Id ([int]$pair[0]) -ErrorAction SilentlyContinue
                    if($process){
                        try{
                            $started=$process.StartTime
                            if($null -eq $started -or $started.ToUniversalTime().Ticks -eq [long]$pair[1]){$active=$true}
                        }catch{$active=$true}
                    }
                }
            }
        }
        $browserProcesses = Get-CimInstance Win32_Process -Filter "Name='chrome.exe' OR Name='msedge.exe'"
        if(@($browserProcesses | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($absolute+[IO.Path]::DirectorySeparatorChar) }).Count -gt 0){$active=$true}
        if($active){continue}
        # Identity, resolved absolute parent, GUID, no active processes, and no reparse root checked above.
        $bytesFreed+=((Get-ChildItem -LiteralPath $absolute -File -Recurse | Measure-Object Length -Sum).Sum)
        Remove-Item -LiteralPath $absolute -Recurse -Force
        $removed++
    }
}
$sdkArchive=[IO.Path]::GetFullPath((Join-Path $projectRoot '.tools\dotnet-sdk.zip'))
$expectedTools=[IO.Path]::GetFullPath((Join-Path $projectRoot '.tools'))
if([IO.Path]::GetDirectoryName($sdkArchive) -eq $expectedTools -and (Test-Path -LiteralPath $sdkArchive)){
    $bytesFreed+=(Get-Item -LiteralPath $sdkArchive).Length
    Remove-Item -LiteralPath $sdkArchive
}
[ordered]@{RemovedSyntheticTestDirectories=$removed;FreedMiB=[Math]::Round($bytesFreed/1MB,1)} | ConvertTo-Json
