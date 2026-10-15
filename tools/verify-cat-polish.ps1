param([string]$OutputFolder='Source\Verification\CatPolish031\Contacts')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=Join-Path $taskRoot $OutputFolder
New-Item -ItemType Directory $taskOutput -Force|Out-Null
$taskArgs=@('--verify-cat-polish','--verification-output',('"'+$taskOutput+'"'),'-force-d3d11','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-logFile',('"'+(Join-Path $taskOutput 'play.log')+'"'))
$taskProcess=Start-Process (Join-Path $taskRoot 'Builds\Windows\CatSimulator.exe') -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
$null=$taskProcess.Handle;$taskProcess.WaitForExit()
Get-Content (Join-Path $taskOutput 'cat-polish.json')
if($taskProcess.ExitCode -ne 0){throw 'Contact and animation verification failed.'}
