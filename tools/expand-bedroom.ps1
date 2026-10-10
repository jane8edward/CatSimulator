param([string]$BlenderPath='E:\SteamLibrary\steamapps\common\Blender\blender.exe',
      [string]$EditorPath='E:\Game\Tools\Unity\6000.3.26f1\Editor\Unity.exe',
      [switch]$SkipModeling)
$ErrorActionPreference='Stop'
$bedroomProjectRoot=Split-Path -Parent $PSScriptRoot
$bedroomEvidence=Join-Path $bedroomProjectRoot 'Source\Verification\Bedroom'
New-Item -ItemType Directory -Path $bedroomEvidence -Force | Out-Null
if(!$SkipModeling) {
    $bedroomModel=Start-Process -FilePath $BlenderPath -ArgumentList '--background','--disable-autoexec',('"'+(Join-Path $bedroomProjectRoot 'Source\Blender\spacious-living-room.blend')+'"'),'--python-exit-code','1','--python',('"'+(Join-Path $bedroomProjectRoot 'Source\Blender\extend_bedroom.py')+'"') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $bedroomEvidence 'blender-expansion.log') -RedirectStandardError (Join-Path $bedroomEvidence 'blender-expansion-errors.log') -Wait -PassThru
    if($bedroomModel.ExitCode -ne 0) {throw 'Bedroom modeling failed. See Source/Verification/Bedroom/blender-expansion.log'}
}
$bedroomBuild=Start-Process -FilePath $EditorPath -ArgumentList '-batchmode','-quit','-nographics','-projectPath',('"'+(Join-Path $bedroomProjectRoot 'Game')+'"'),'-executeMethod','CatSimulator.Editor.FirstPlayableBuilder.ApplyBedroomExpansionAndBuild','-logFile',('"'+(Join-Path $bedroomEvidence 'unity-expansion.log')+'"') -WindowStyle Hidden -Wait -PassThru
if($bedroomBuild.ExitCode -ne 0) {throw 'Bedroom integration/build failed. See Source/Verification/Bedroom/unity-expansion.log'}
Write-Output (Join-Path $bedroomProjectRoot 'Builds\Windows\CatSimulator.exe')
