param([string]$OutputFolder='Source\Verification\GameFlow')
$ErrorActionPreference='Stop'
$flowRoot=Split-Path -Parent $PSScriptRoot
$flowOutput=Join-Path $flowRoot $OutputFolder
New-Item -ItemType Directory -Path $flowOutput -Force | Out-Null
$flowArgs=@('--verify-game-flow','--verification-output',('"'+$flowOutput+'"'),'-force-d3d11','-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',('"'+(Join-Path $flowOutput 'play.log')+'"'))
$flowProcess=Start-Process -FilePath (Join-Path $flowRoot 'Builds\Windows\CatSimulator.exe') -ArgumentList $flowArgs -WindowStyle Hidden -PassThru
$null=$flowProcess.Handle
$flowProcess.WaitForExit()
Get-Content -LiteralPath (Join-Path $flowOutput 'game-flow.json')
if($null -eq $flowProcess.ExitCode -or $flowProcess.ExitCode -ne 0){throw 'Full game verification failed; see game-flow.json'}
