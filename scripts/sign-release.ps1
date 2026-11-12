[CmdletBinding()]
param([switch]$InitializeKey,[switch]$IncludeMac)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Security.Cryptography.ProtectedData
if(-not ('ClaudeLinkReleaseSigning' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'ReleaseSigning.cs')}
$keyRoot=Join-Path $env:LOCALAPPDATA 'ClaudeLinkRelease'
$keyPath=Join-Path $keyRoot 'signing-key.dat'
if(-not(Test-Path -LiteralPath $keyPath)){
    if(-not $InitializeKey){throw 'Release key missing. Initialize once, embed the public key, and retain the protected local key.'}
    New-Item -ItemType Directory -Path $keyRoot -Force | Out-Null
    $privateBytes=[ClaudeLinkReleaseSigning]::NewKey()
    [IO.File]::WriteAllBytes($keyPath,[Security.Cryptography.ProtectedData]::Protect($privateBytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser))
}else{
    $privateBytes=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($keyPath),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
}
try{
    $publicKey=[ClaudeLinkReleaseSigning]::PublicKey($privateBytes)
    if($InitializeKey){$publicKey;return}
    $updaterSource=[IO.File]::ReadAllText((Join-Path $projectRoot 'src\Launcher.App\Services\ReleaseUpdater.cs'))
    $embeddedKey=[regex]::Match($updaterSource,'public const string PublicKey = "([^"]+)"').Groups[1].Value
    if($publicKey -cne $embeddedKey){throw 'Signing key does not match the embedded update trust key.'}
    [xml]$project=[IO.File]::ReadAllText((Join-Path $projectRoot 'src\Launcher.App\Launcher.App.csproj'))
    $version=[string]$project.Project.PropertyGroup.Version
    if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid release version.'}
    $assetName='ClaudeLink-Setup-'+$version+'.exe'
    $artifacts=Join-Path $projectRoot 'artifacts'
    $setup=Get-Item -LiteralPath (Join-Path $artifacts $assetName)
    $manifest=[ordered]@{Schema=1;Repository='slobys/ClaudeLink';Version=$version;AssetName=$assetName;Size=$setup.Length;Sha256=(Get-FileHash -LiteralPath $setup.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    $payload=[Text.UTF8Encoding]::new($false).GetBytes(($manifest | ConvertTo-Json))
    $signature=[ClaudeLinkReleaseSigning]::Sign($privateBytes,$payload)
    [IO.File]::WriteAllBytes((Join-Path $artifacts 'update.json'),$payload)
    [IO.File]::WriteAllText((Join-Path $artifacts 'update.json.sig'),[Convert]::ToBase64String($signature),[Text.UTF8Encoding]::new($false))
    if($IncludeMac){
        $macSource=[IO.File]::ReadAllText((Join-Path $projectRoot 'src/Launcher.Mac/Updater.swift'))
        if(-not $macSource.Contains($publicKey)){throw 'Mac trust key differs from the release signing key.'}
        foreach($architecture in @('arm64','x64')){
            $macAsset='ClaudeLink-'+$version+'-macos-'+$architecture+'.zip'
            $macFile=Get-Item -LiteralPath (Join-Path $artifacts $macAsset)
            $macManifest=[ordered]@{Schema=1;Repository='slobys/ClaudeLink';Version=$version;AssetName=$macAsset;Size=$macFile.Length;Sha256=(Get-FileHash -LiteralPath $macFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
            $macPayload=[Text.UTF8Encoding]::new($false).GetBytes(($macManifest | ConvertTo-Json))
            $macName='update-macos-'+$architecture+'.json'
            [IO.File]::WriteAllBytes((Join-Path $artifacts $macName),$macPayload)
            [IO.File]::WriteAllText((Join-Path $artifacts ($macName+'.sig')),[Convert]::ToBase64String([ClaudeLinkReleaseSigning]::Sign($privateBytes,$macPayload)),[Text.UTF8Encoding]::new($false))
        }
    }
    @{Version=$version;SignedManifest='update.json';PrivateKeyPublished=$false} | ConvertTo-Json
}finally{[Array]::Clear($privateBytes,0,$privateBytes.Length)}
