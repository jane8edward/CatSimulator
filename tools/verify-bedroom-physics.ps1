param([string]$OutputFolder='Source\Verification\BedroomPhysics')
$ErrorActionPreference='Stop'
$physicsProjectRoot=Split-Path -Parent $PSScriptRoot
$physicsOutput=Join-Path $physicsProjectRoot $OutputFolder
New-Item -ItemType Directory -Path $physicsOutput -Force | Out-Null
$physicsArgs=@('--verify-bedroom-physics','--verification-output',('"'+$physicsOutput+'"'),'-screen-width','1920','-screen-height','1080','-screen-fullscreen','1','-logFile',('"'+(Join-Path $physicsOutput 'bedroom-physics-play.log')+'"'))
$physicsProcess=Start-Process -FilePath (Join-Path $physicsProjectRoot 'Builds\Windows\CatSimulator.exe') -ArgumentList $physicsArgs -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $physicsOutput 'bedroom-physics.json')
if($physicsProcess.ExitCode -ne 0) { throw 'Bedroom physics verification failed. See Source/Verification/BedroomPhysics/bedroom-physics.json' }
