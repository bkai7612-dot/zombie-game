#!/usr/bin/env bash
# Verification that does NOT need a Unity install:
#   1. runs the engine-independent rule tests (NUnit, dotnet test)
#   2. type-checks every script assembly against real UnityEngine/UnityEditor reference assemblies and
#      signature stubs for Netcode for GameObjects 2.4.4 and Unity Gaming Services.
# It cannot prove the game runs - see docs/TESTING.md for the in-Unity checks.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"

echo "== Core rules: unit tests"
dotnet test "$here/CoreTests/CoreTests.csproj" -nologo -v q

echo "== Unity reference assemblies"
"$here/UnityCompileCheck/fetch-unity-refs.sh"

for p in Gameplay UI AssemblyCSharp Editor PlayModeTests; do
  echo "== Compile check: $p"
  dotnet build "$here/UnityCompileCheck/$p.csproj" -nologo -v q
done
echo "All verification steps passed."
