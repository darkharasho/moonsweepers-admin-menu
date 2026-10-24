#!/usr/bin/env bash
# Builds a Release DLL and zips a Thunderstore/r2modman package into dist/.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
manifest="$root/thunderstore/manifest.json"
stage="$root/dist/stage"

# Validate the manifest and check the version matches the plugin and project.
version="$(python3 - "$manifest" "$root" <<'PY'
import json, re, sys, pathlib
m = json.load(open(sys.argv[1]))
root = pathlib.Path(sys.argv[2])
errors = []
if not re.fullmatch(r"[A-Za-z0-9_]+", m.get("name", "")):
    errors.append("name must be letters, digits and underscores")
if not re.fullmatch(r"\d+\.\d+\.\d+", m.get("version_number", "")):
    errors.append("version_number must be Major.Minor.Patch")
if len(m.get("description", "")) > 250:
    errors.append("description is over 250 characters")
v = m.get("version_number")
plugin = re.search(r'PluginVersion = "([^"]+)"', (root / "src/AdminMenu/Plugin.cs").read_text())
csproj = re.search(r"<Version>([^<]+)</Version>", (root / "src/AdminMenu/AdminMenu.csproj").read_text())
if not plugin or plugin.group(1) != v:
    errors.append(f"Plugin.cs PluginVersion is {plugin and plugin.group(1)}, manifest is {v}")
if not csproj or csproj.group(1) != v:
    errors.append(f"AdminMenu.csproj Version is {csproj and csproj.group(1)}, manifest is {v}")
if f"## {v}" not in (root / "CHANGELOG.md").read_text():
    errors.append(f"CHANGELOG.md has no '## {v}' entry")
if errors:
    sys.exit("manifest check failed:\n  " + "\n  ".join(errors))
print(v)
PY
)"
zipfile="$root/dist/darkharasho-MoonSweepersAdminMenu-$version.zip"

dotnet build "$root/src/AdminMenu/AdminMenu.csproj" -c Release

rm -rf "$stage" "$zipfile"
mkdir -p "$stage/plugins"
cp "$manifest" "$root/thunderstore/icon.png" "$root/thunderstore/README.md" "$root/CHANGELOG.md" "$stage/"
cp "$root/src/AdminMenu/bin/Release/AdminMenu.dll" "$stage/plugins/"

(cd "$stage" && zip -qrX "$zipfile" manifest.json icon.png README.md CHANGELOG.md plugins)
rm -rf "$stage"
unzip -l "$zipfile"
echo "Package: $zipfile"
