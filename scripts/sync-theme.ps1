$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$tokens=Get-Content -LiteralPath (Join-Path $projectRoot 'design\tokens.json') -Raw | ConvertFrom-Json
$lines=@('<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">')
foreach($entry in $tokens.PSObject.Properties){$lines+=' <SolidColorBrush x:Key="'+$entry.Name+'" Color="'+$entry.Value+'"/>'}
$lines+='</ResourceDictionary>'
$themeDirectory=Join-Path $projectRoot 'src\Launcher.App\Theme'
New-Item -ItemType Directory -Path $themeDirectory -Force | Out-Null
[IO.File]::WriteAllLines((Join-Path $themeDirectory 'Colors.xaml'),$lines,[Text.UTF8Encoding]::new($false))
