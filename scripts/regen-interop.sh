#!/usr/bin/env bash
# Regenerates the IL2CPP interop assemblies after a game update and installs them into the HML profile.
# BepInEx's own generation is off (UpdateInteropAssemblies = false) because ours injects Cpp2IL attributes
# for correct struct offsets, so a game update leaves stale interop and the chainloader fails before any plugin
# loads ("Interop assemblies are possibly out of date", then an IL2CPP exception in SetupUnityLogging).
#
# Usage: scripts/regen-interop.sh [out-name]   (default interop-<buildid>, under ~/.cache/ms-il2cpp)
# Needs the Cpp2IL and Il2CppInterop.CLI builds in ~/.cache/ms-il2cpp/src-*. Waits for the game to exit before installing.
set -euo pipefail

cache=~/.cache/ms-il2cpp
game="${GAME_PATH:-/var/mnt/data/SteamLibrary/steamapps/common/Moon Sweepers}"
profile="${PROFILE_PATH:-$HOME/.config/hml/games/steam-5335510/profiles/default}/BepInEx"
build=$(grep -oP '"buildid"\s+"\K[0-9]+' "$game/../../appmanifest_5335510.acf")
out="${1:-interop-$build}"
export DOTNET_ROOT="${DOTNET_ROOT:-/home/linuxbrew/.linuxbrew/opt/dotnet/libexec}" DOTNET_ROLL_FORWARD=Major
run() { nice -n 19 taskset -c 0-3 "$@"; }

cd "$cache"
rm -rf "dummy-$build" "$out"
run dotnet src-cpp2il/Cpp2IL/bin/Release/net10.0/Cpp2IL.dll --game-path "$game" --exe-name MoonSweepers \
  --use-processor attributeinjector --output-as dummydll --output-to "./dummy-$build" > "cpp2il-$build.log" 2>&1
run dotnet src-interop/bin/Il2CppInterop.CLI/net6.0/Il2CppInterop.CLI.dll generate --input "dummy-$build" --output "$out" \
  --unity "$profile/unity-libs" --game-assembly "$game/GameAssembly.dll" --no-parallel > "$out.log" 2>&1
grep -E 'unstrip statistics|Done!' "$out.log"
ln -sfn "$out" interop-current  # the csproj's default InteropPath

while pgrep -f 'MoonSweepers\.ex[e]' > /dev/null; do echo "waiting for the game to exit"; sleep 5; done
# BepInEx's hash of the game files is only printed by a launch, so keep the profile's and let a stale one just warn.
hash=$(cat "$profile/interop/assembly-hash.txt" 2>/dev/null || true)
rm -rf "$profile/interop.old"
mv "$profile/interop" "$profile/interop.old"
cp -r "$out" "$profile/interop"
[ -n "$hash" ] && printf %s "$hash" > "$profile/interop/assembly-hash.txt"
echo "installed $cache/$out into $profile/interop (previous set in interop.old)"
