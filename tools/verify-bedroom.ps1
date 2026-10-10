param([string]$OutputFolder='Source\Verification\Bedroom')
$ErrorActionPreference='Stop'
$bedroomProjectRoot=Split-Path -Parent $PSScriptRoot
$bedroomOutput=Join-Path $bedroomProjectRoot $OutputFolder
New-Item -ItemType Directory -Path $bedroomOutput -Force | Out-Null
$bedroomArgs=@('--verify-bedroom','--verification-output',('"'+$bedroomOutput+'"'),'-screen-width','1920','-screen-height','1080','-screen-fullscreen','1','-logFile',('"'+(Join-Path $bedroomOutput 'bedroom-play.log')+'"'))
$bedroomTest=Start-Process -FilePath (Join-Path $bedroomProjectRoot 'Builds\Windows\CatSimulator.exe') -ArgumentList $bedroomArgs -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $bedroomOutput 'bedroom-verification.json')
if($bedroomTest.ExitCode -ne 0){throw 'Bedroom validation failed. See Source/Verification/Bedroom/bedroom-verification.json'}
