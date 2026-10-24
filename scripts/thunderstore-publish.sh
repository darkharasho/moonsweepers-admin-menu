#!/usr/bin/env bash
# Publishes a Thunderstore package zip with tcli. Safe to rerun: skips versions that are
# already on Thunderstore, and skips with a warning when no token is set.
#
# Usage: thunderstore-publish.sh <zip> --community <slug> [--namespace <team>] [--categories a,b]
#
#   <zip>         Package zip with manifest.json at its root.
#   --community   Thunderstore community slug, e.g. repo, stonewards, roadside-research.
#   --namespace   Thunderstore team. Default: darkharasho.
#   --categories  Comma-separated category slugs. Default: the categories the package already
#                 has on Thunderstore, so edits made on the website are kept. Required for a
#                 package's first upload.
#
# Env: THUNDERSTORE_TOKEN (service account token). In GitHub Actions, pass the repo secret.
set -euo pipefail

zip="" community="" namespace="darkharasho" categories=""
while [ $# -gt 0 ]; do
    case "$1" in
        --community) community="$2"; shift 2 ;;
        --namespace) namespace="$2"; shift 2 ;;
        --categories) categories="$2"; shift 2 ;;
        -*) echo "Unknown option: $1" >&2; exit 2 ;;
        *) zip="$1"; shift ;;
    esac
done
[ -f "$zip" ] || { echo "Package zip not found: $zip" >&2; exit 2; }
zip="$(realpath "$zip")"
[ -n "$community" ] || { echo "--community is required" >&2; exit 2; }

if [ -z "${THUNDERSTORE_TOKEN:-}" ]; then
    echo "::warning::THUNDERSTORE_TOKEN is not set; skipping Thunderstore publish"
    exit 0
fi

read -r name version < <(unzip -p "$zip" manifest.json | python3 -c \
    "import json,sys; m=json.load(sys.stdin); print(m['name'], m['version_number'])")
api="https://thunderstore.io/api"

status="$(curl -s -o /dev/null -w '%{http_code}' "$api/experimental/package/$namespace/$name/$version/")"
if [ "$status" = "200" ]; then
    echo "$namespace-$name-$version is already on Thunderstore; skipping"
    exit 0
fi

if [ -z "$categories" ]; then
    categories="$(curl -sf "$api/cyberstorm/listing/$community/$namespace/$name/" | python3 -c \
        "import json,sys; print(','.join(c['slug'] for c in json.load(sys.stdin)['categories']))" 2>/dev/null || true)"
    if [ -z "$categories" ]; then
        echo "No existing Thunderstore listing for $namespace-$name in $community; pass --categories" >&2
        exit 1
    fi
    echo "Keeping existing categories: $categories"
fi

if ! command -v tcli >/dev/null 2>&1; then
    dotnet tool install -g tcli >/dev/null
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

workdir="$(mktemp -d)"
trap 'rm -rf "$workdir"' EXIT
toml_list() { python3 -c "import sys; print(', '.join(repr(s) for s in sys.argv[1].split(',') if s))" "$1"; }
cat > "$workdir/thunderstore.toml" <<EOF
[config]
schemaVersion = "0.0.1"

[package]
namespace = "$namespace"
name = "$name"
versionNumber = "$version"
description = ""
websiteUrl = ""
containsNsfwContent = false
[package.dependencies]

[publish]
repository = "https://thunderstore.io"
communities = ["$community"]
[publish.categories]
"$community" = [$(toml_list "$categories")]
EOF

echo "Publishing $namespace-$name-$version to $community ($categories)"
(cd "$workdir" && tcli publish --file "$zip" --token "$THUNDERSTORE_TOKEN")
