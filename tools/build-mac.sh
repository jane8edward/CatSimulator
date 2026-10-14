#!/bin/bash
set -euo pipefail
task_root="$(cd "$(dirname "$0")/.." && pwd)"
task_editor="${UNITY_EDITOR:-/Applications/Unity/Unity-6000.3.26f1/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$task_editor" ]]; then
    echo "Set UNITY_EDITOR to your Unity 6000.3.26f1 executable." >&2
    exit 1
fi
mkdir -p "$task_root/Source/Verification/ModelResources"
"$task_editor" -batchmode -quit -nographics -projectPath "$task_root/Game" \
    -executeMethod CatSimulator.Editor.ModelResourceIntegration.BuildMac \
    -logFile "$task_root/Source/Verification/ModelResources/mac-build.log"
echo "$task_root/Builds/Mac/CatSimulator.app"
