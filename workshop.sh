#!/bin/bash
# Uploads the Slime Faction mod to the Steam Workshop via steamcmd.
#
# Usage: ./workshop.sh ["change note"]
# Login: set STEAM_USER env var, or put your Steam username on the first
#        line of ~/.steam_user (chmod 600). Steam Guard code / password
#        prompts are handled interactively by steamcmd itself.
set -e
cd "$(dirname "$0")"

NOTE="${1:-Update $(date +%Y-%m-%d)}"
USER="${STEAM_USER:-$(head -1 ~/.steam_user 2>/dev/null)}"
if [ -z "$USER" ]; then
  echo "ERROR: no Steam username. Set STEAM_USER or create ~/.steam_user"
  exit 1
fi

# 1. Build fresh
./build.sh

# 2. Stage a clean content folder (no source, no .bak files, no git)
STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT
for d in About Assemblies Defs Languages Patches Settings Textures Sounds News; do
  [ -d "$d" ] && cp -r "$d" "$STAGE/"
done
find "$STAGE" -name "*.bak*" -delete

# 3. Render the VDF and upload
VDF=$(mktemp --suffix=.vdf)
sed -e "s|__CONTENTDIR__|$STAGE/|" \
    -e "s|__CHANGENOTE__|$NOTE|" \
    workshop.vdf.template > "$VDF"
rm -f "$VDF.log"

~/steamcmd/steamcmd.sh +login "$USER" \
  +workshop_build_item "$VDF" \
  +quit

echo "Workshop upload finished for published file 3786764216."
