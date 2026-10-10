$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskGame = Join-Path $taskRoot 'Builds\Windows\CatSimulator.exe'
if (!(Test-Path -LiteralPath $taskGame)) { throw 'Build the game with tools/build.ps1 first.' }
Start-Process -FilePath $taskGame -WorkingDirectory (Split-Path -Parent $taskGame)
