#!/usr/bin/env bash
# Downloads Unity reference assemblies (UnityEngine modules + UnityEditor) from NuGet into ./refs.
# These are public metadata-only packages; they let us type-check scripts without installing Unity.
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p refs/engine refs/editor
if [ ! -f refs/engine/UnityEngine.CoreModule.dll ]; then
  curl -sSL -o refs/modules.nupkg https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  unzip -q -o -j refs/modules.nupkg 'lib/net45/*.dll' -d refs/engine
fi
if [ ! -f refs/editor/UnityEditor.dll ]; then
  curl -sSL -o refs/sdk.nupkg https://api.nuget.org/v3-flatcontainer/unity3d.sdk/2021.1.14.1/unity3d.sdk.2021.1.14.1.nupkg
  unzip -q -o -j refs/sdk.nupkg 'lib/UnityEditor.dll' -d refs/editor
fi
ls refs/engine | wc -l
