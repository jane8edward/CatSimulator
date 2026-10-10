param([string]$BlenderPath='E:\SteamLibrary\steamapps\common\Blender\blender.exe',
      [string]$EditorPath='E:\Game\Tools\Unity\6000.3.26f1\Editor\Unity.exe',
      [switch]$SkipConversion)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$catSource=Join-Path $taskRoot 'assets\BlendSwap18519\cat.blend'
if(!(Test-Path -LiteralPath $catSource)) {throw "Source model missing: $catSource"}
if(!$SkipConversion) {
    & $BlenderPath --background --disable-autoexec $catSource --python-exit-code 1 --python (Join-Path $taskRoot 'Source\Blender\convert_blendswap_cat.py') *> (Join-Path $taskRoot 'Source\Verification\blendswap18519-convert.log')
    if($LASTEXITCODE -ne 0) {throw 'Cat conversion failed. See Source/Verification/blendswap18519-convert.log'}
}
$taskLog=Join-Path $taskRoot 'Source\unity-cat-integration.log'
$taskProject=Join-Path $taskRoot 'Game'
$taskBuild=Start-Process -FilePath $EditorPath -ArgumentList '-batchmode','-quit','-nographics','-projectPath',('"'+$taskProject+'"'),'-executeMethod','CatSimulator.Editor.BlendSwapCatIntegration.InstallAndBuild','-logFile',('"'+$taskLog+'"') -WindowStyle Hidden -Wait -PassThru
if($taskBuild.ExitCode -ne 0) {throw "Cat integration or build failed. See $taskLog"}
Write-Output (Join-Path $taskRoot 'Builds\Windows\CatSimulator.exe')
