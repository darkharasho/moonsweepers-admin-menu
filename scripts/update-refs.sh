#!/usr/bin/env bash
# Regenerates lib/refs: reference-only copies (public API signatures, no method bodies) of the
# game and BepInEx assemblies, so CI can build without the game installed.
# Needs the refasmer tool: dotnet tool install -g JetBrains.Refasmer.CliTool
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
game_root="${GAME_PATH:-/var/mnt/data/SteamLibrary/steamapps/common/Moon Sweepers}"
game="$game_root/MoonSweepers_Data/Managed"
# Any BepInEx 5.4 core works. Falls back to the Stonewards r2modman profile if BepInEx isn't in the game folder.
bepinex="${BEPINEX_CORE:-$game_root/BepInEx/core}"
[ -f "$bepinex/BepInEx.dll" ] || bepinex="$HOME/.config/r2modmanPlus-local/Stonewards/profiles/Default/BepInEx/core"
out="$root/lib/refs"

# Keep in sync with the <Reference> items in src/AdminMenu/AdminMenu.csproj.
game_dlls=(Assembly-CSharp com.rlabrecque.steamworks.net UnityEngine UnityEngine.CoreModule UnityEngine.IMGUIModule UnityEngine.TextRenderingModule UnityEngine.UI Unity.InputSystem)
bepinex_dlls=(BepInEx 0Harmony)

rm -rf "$out"
mkdir -p "$out"
for name in "${game_dlls[@]}"; do refasmer -q -c --omit-non-api-members=true -O "$out" "$game/$name.dll"; done
for name in "${bepinex_dlls[@]}"; do refasmer -q -c --omit-non-api-members=true -O "$out" "$bepinex/$name.dll"; done
ls -l "$out"
