#!/usr/bin/env bash
# Regenerates lib/refs: reference-only copies (public API signatures, no method bodies) of the game's
# interop assemblies and the BepInEx 6 IL2CPP core, so CI can build without the game installed.
# Needs the refasmer tool: dotnet tool install -g JetBrains.Refasmer.CliTool
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
# Our generated interop for the current game build (see regen-interop.sh), and the profile's BepInEx core.
profile="${PROFILE_PATH:-$HOME/.config/hml/games/steam-5335510/profiles/default}"
interop="${INTEROP_PATH:-$HOME/.cache/ms-il2cpp/interop-current}"
bepinex="${BEPINEX_CORE:-$profile/BepInEx/core}"
out="$root/lib/refs"

# Keep in sync with the <Reference> items in src/AdminMenu/AdminMenu.csproj.
interop_dlls=(Il2Cppmscorlib Assembly-CSharp com.rlabrecque.steamworks.net UnityEngine.CoreModule UnityEngine.IMGUIModule UnityEngine.PhysicsModule UnityEngine.TextRenderingModule UnityEngine.UI Unity.InputSystem)
bepinex_dlls=(BepInEx.Core BepInEx.Unity.IL2CPP BepInEx.Unity.Common 0Harmony Il2CppInterop.Runtime Il2CppInterop.Common)

rm -rf "$out"
mkdir -p "$out"
for name in "${interop_dlls[@]}"; do refasmer -q -c --omit-non-api-members=true -O "$out" "$interop/$name.dll"; done
for name in "${bepinex_dlls[@]}"; do refasmer -q -c --omit-non-api-members=true -O "$out" "$bepinex/$name.dll"; done
ls -l "$out"
