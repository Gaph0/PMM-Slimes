#!/bin/bash
# Creates a GitHub Release for the Slime Faction mod: builds the mod, zips
# the distributable files, and uploads the zip as a release asset.
#
# Usage:   ./release.sh v1.0.0 ["release notes"]
# Auth:    put a GitHub personal access token (repo scope) in the
#          GITHUB_TOKEN env var, or in ~/.github_token (chmod 600).
#          Create one at https://github.com/settings/tokens
set -e
cd "$(dirname "$0")"

REPO="Gaph0/PMM---Slimes"
MOD="PMM-SlimeFaction"
TAG="$1"
NOTES="${2:-Release $TAG}"

if [ -z "$TAG" ]; then
  echo "Usage: ./release.sh v1.0.0 [\"release notes\"]"
  exit 1
fi

TOKEN="${GITHUB_TOKEN:-$(cat ~/.github_token 2>/dev/null)}"
if [ -z "$TOKEN" ]; then
  echo "ERROR: no GitHub token. Set GITHUB_TOKEN or create ~/.github_token"
  exit 1
fi

# 1. Build (must succeed before we ship anything)
./build.sh

# 2. Zip the mod folder the way the Steam Workshop layout expects it
ZIP="$MOD-$TAG.zip"
rm -f "$ZIP"
for d in About Assemblies Defs Languages Patches Settings Textures Sounds News README.md; do
  [ -e "$d" ] && zip -qr "$ZIP" "$d" -x "Assemblies/*.bak*"
done

# 3. Create the release
response=$(curl -sf -X POST \
  -H "Authorization: token $TOKEN" \
  -H "Accept: application/vnd.github+json" \
  "https://api.github.com/repos/$REPO/releases" \
  -d "{\"tag_name\":\"$TAG\",\"name\":\"$TAG\",\"body\":\"$NOTES\"}")
upload_url=$(echo "$response" | sed -n 's/.*"upload_url": *"\([^"{]*\).*/\1/p')

if [ -z "$upload_url" ]; then
  echo "ERROR: release creation failed:"
  echo "$response"
  exit 1
fi

# 4. Upload the asset
curl -sf -X POST \
  -H "Authorization: token $TOKEN" \
  -H "Content-Type: application/zip" \
  "$upload_url?name=$ZIP" \
  --data-binary "@$ZIP" > /dev/null

echo "Released $TAG with $ZIP: https://github.com/$REPO/releases/tag/$TAG"
rm -f "$ZIP"
