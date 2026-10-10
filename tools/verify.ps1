param([switch]$RecordPreview)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskGame=Join-Path $taskRoot 'Builds\Windows\CatSimulator.exe'
$taskOutput=Join-Path $taskRoot 'Source\Verification'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
foreach($taskOrder in @('cup-first','curtain-first'))
{
    $taskArgs=@('--verify-playable','--verification-output',('"'+$taskOutput+'"'),'-screen-width','1920','-screen-height','1080','-screen-fullscreen','1','-logFile',('"'+(Join-Path $taskOutput ($taskOrder+'.log'))+'"'))
    if($taskOrder -eq 'curtain-first') {$taskArgs+='--curtain-first'}
    if($RecordPreview -and $taskOrder -eq 'cup-first') {$taskArgs+='--record-preview'}
    $taskProcess=Start-Process -FilePath $taskGame -ArgumentList $taskArgs -WindowStyle Normal -Wait -PassThru
    if($taskProcess.ExitCode -ne 0) {throw "Verification failed: $taskOrder"}
    Get-Content -LiteralPath (Join-Path $taskOutput ($taskOrder+'.json'))
}
