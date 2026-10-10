param([string]$EditorPath = 'E:\Game\Tools\Unity\6000.3.26f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath $EditorPath)) { throw "Unity Editor missing: $EditorPath" }
$taskLog = Join-Path $taskRoot 'Source\unity-build.log'
$taskProject = Join-Path $taskRoot 'Game'
$taskBuild = Start-Process -FilePath $EditorPath -ArgumentList '-batchmode','-quit','-nographics','-projectPath',('"'+$taskProject+'"'),'-executeMethod','CatSimulator.Editor.FirstPlayableBuilder.Build','-logFile',('"'+$taskLog+'"') -WindowStyle Hidden -Wait -PassThru
if ($taskBuild.ExitCode -ne 0) { throw "Build failed. See $taskLog" }
Write-Output (Join-Path $taskRoot 'Builds\Windows\CatSimulator.exe')
