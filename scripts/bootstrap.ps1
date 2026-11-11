$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$toolsRoot=Join-Path $projectRoot '.tools'
$vendorRoot=Join-Path $projectRoot 'vendor'
New-Item -ItemType Directory -Force -Path $toolsRoot,$vendorRoot | Out-Null
$sdkVersion='10.0.404'
$sdkRoot=Join-Path $toolsRoot 'dotnet'
if (-not (Test-Path -LiteralPath (Join-Path $sdkRoot 'dotnet.exe'))) {
    $metadata=Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $sdk=@($metadata.releases | ForEach-Object { $_.sdks } | Where-Object { $_.version -eq $sdkVersion })[0]
    $sdkFile=$sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
    if (-not $sdkFile) { throw 'Pinned SDK not found in official release metadata.' }
    $sdkArchive=Join-Path $toolsRoot 'dotnet-sdk.zip'
    if (-not (Test-Path -LiteralPath $sdkArchive)) { Invoke-WebRequest $sdkFile.url -OutFile $sdkArchive }
    if ((Get-FileHash -LiteralPath $sdkArchive -Algorithm SHA512).Hash -ne $sdkFile.hash) { throw 'SDK SHA512 mismatch.' }
    New-Item -ItemType Directory -Force -Path $sdkRoot | Out-Null
    Expand-Archive -LiteralPath $sdkArchive -DestinationPath $sdkRoot -Force
}
$coreVersion='1.14.4'
$coreArchive=Join-Path $toolsRoot "sing-box-$coreVersion.zip"
$coreUrl="https://github.com/SagerNet/sing-box/releases/download/v$coreVersion/sing-box-$coreVersion-windows-amd64.zip"
$archiveHash='99185d75d314a07dbba4a130c8ad986cbeb629a07927975636b341ca61f991df'
if (-not (Test-Path -LiteralPath $coreArchive)) { Invoke-WebRequest $coreUrl -OutFile $coreArchive }
if ((Get-FileHash -LiteralPath $coreArchive -Algorithm SHA256).Hash -ne $archiveHash) { throw 'Core SHA256 mismatch.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($coreArchive)
try {
    $entry=$archive.Entries | Where-Object { $_.Name -eq 'sing-box.exe' } | Select-Object -First 1
    if (-not $entry) { throw 'Core executable missing.' }
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $vendorRoot 'sing-box.exe'),$true)
} finally { $archive.Dispose() }
$coreHash=(Get-FileHash -LiteralPath (Join-Path $vendorRoot 'sing-box.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest=[ordered]@{ version=$coreVersion; url=$coreUrl; archiveSha256=$archiveHash; exeSha256=$coreHash; source="https://github.com/SagerNet/sing-box/tree/v$coreVersion" }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $vendorRoot 'core-manifest.json') -Encoding UTF8
if (-not (Test-Path -LiteralPath (Join-Path $vendorRoot 'sing-box-LICENSE.txt'))) {
    Invoke-WebRequest "https://raw.githubusercontent.com/SagerNet/sing-box/v$coreVersion/LICENSE" -OutFile (Join-Path $vendorRoot 'sing-box-LICENSE.txt')
}
Write-Output "SDK $sdkVersion and sing-box $coreVersion verified."
