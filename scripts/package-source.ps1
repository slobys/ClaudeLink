$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$artifacts=Join-Path $projectRoot 'artifacts'
$destination=Join-Path $artifacts 'ClaudeLink-Source-1.1.1.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
if(Test-Path -LiteralPath $destination){Remove-Item -LiteralPath $destination}
$archive=[IO.Compression.ZipFile]::Open($destination,[IO.Compression.ZipArchiveMode]::Create)
try{
    $files=@(Get-Item -LiteralPath (Join-Path $projectRoot 'README.md'),(Join-Path $projectRoot '.gitignore'),(Join-Path $projectRoot 'LICENSE'))
    foreach($name in @('src','tests','scripts','installer','docs','vendor','.github')){
        $path=Join-Path $projectRoot $name
        if(Test-Path -LiteralPath $path){
            $files+=@(Get-ChildItem -LiteralPath $path -File -Recurse | Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Name -ne 'sing-box.exe'
            })
        }
    }
    foreach($file in $files){
        $relative=[IO.Path]::GetRelativePath($projectRoot,$file.FullName).Replace('\','/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)
    }
    foreach($name in @('validation-summary.json','integration-results.json','browser-language-results.json','browser-language-window-results.json','desktop-update-results.json','desktop-launch-results.json','installer-results.json','ui-check/ui-results.json','test-configs/results.json','SHA256.json')){
        $path=Join-Path $artifacts $name
        if(Test-Path -LiteralPath $path){
            if($name -eq 'integration-results.json'){
                $proof=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
                $proof.PSObject.Properties.Remove('TestData')
                $entry=$archive.CreateEntry(('validation/'+$name),[IO.Compression.CompressionLevel]::Optimal)
                $writer=[IO.StreamWriter]::new($entry.Open(),[Text.UTF8Encoding]::new($false))
                try{$writer.Write(($proof | ConvertTo-Json -Depth 15))}finally{$writer.Dispose()}
            }else{[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$path,('validation/'+$name),[IO.Compression.CompressionLevel]::Optimal)}
        }
    }
}finally{$archive.Dispose()}
Get-FileHash -LiteralPath $destination -Algorithm SHA256 | Select-Object Hash,Path | ConvertTo-Json
