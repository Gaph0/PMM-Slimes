#!/bin/bash
# Syncs the mod into RimWorld's local Mods folder for in-game testing.
# Excludes dev-only files (git, wiki, backups, plans) so the in-game copy is clean.
set -e
cd "$(dirname "$0")"

# --- Stale-assembly guard -------------------------------------------------
# A failed compile used to be silent: this script still copied the old DLL to the
# game, so the mod ran old code. Refuse to deploy while any source file is newer
# than the built assembly. Rebuild with ./build.sh and check it exits 0. Set
# PMM_SYNC_FORCE=1 to deploy anyway (safe for XML-only work, which needs no build).
if [ "${PMM_SYNC_FORCE:-0}" != "1" ]; then
  DLL=$(ls -t Assemblies/*.dll 2>/dev/null | head -1)
  if [ -z "$DLL" ]; then
    echo "sync.sh: REFUSING to deploy - no assembly in Assemblies/." >&2
    echo "Run ./build.sh first, or set PMM_SYNC_FORCE=1 to deploy anyway." >&2
    exit 1
  fi
  NEWER=$(find Source -name '*.cs' -newer "$DLL" -print -quit 2>/dev/null)
  if [ -n "$NEWER" ]; then
    echo "sync.sh: REFUSING to deploy - the assembly is older than a source file." >&2
    echo "  source   : $NEWER" >&2
    echo "  assembly : $DLL" >&2
    echo "Run ./build.sh and check it exits 0, or set PMM_SYNC_FORCE=1 to deploy anyway." >&2
    exit 1
  fi
fi

DEST="$HOME/.steam/steam/steamapps/common/RimWorld/Mods/Project Momo Slime Faction"
mkdir -p "$DEST"

# --- What gets deployed -------------------------------------------------
# Only what the game and the player need:
#   game content: About, Assemblies, Defs, Patches, Languages, Textures,
#                 Sounds, News, Settings, LoadFolders.xml
#   player docs : README.md, CHANGELOG.md
# Everything else in this repo is dev-only: Source, Tools, reports, plans and
# other *.md notes, build scripts, project files, backups, .git and hooks, IDE
# folders, Python venvs, art sources and zips.
# The list matches release.sh, so the deployed folder and the release zip hold
# the same files. --delete-excluded also removes dev leftovers from earlier
# syncs: plain --delete cannot, because rsync never deletes a path it has been
# told to exclude.
rsync -a --delete --delete-excluded \
  --include='/README.md' \
  --include='/CHANGELOG.md' \
  --exclude='/*.md' \
  --exclude='.git' \
  --exclude='.githooks' \
  --exclude='.gitignore' \
  --exclude='.vscode' \
  --exclude='.venv' \
  --exclude='Source' \
  --exclude='Tools' \
  --exclude='reports' \
  --exclude='Wiki' \
  --exclude='bin' \
  --exclude='obj' \
  --exclude='*.csproj' \
  --exclude='*.sln' \
  --exclude='*.props' \
  --exclude='global.json' \
  --exclude='NuGet.config' \
  --exclude='*.sh' \
  --exclude='*.bak*' \
  --exclude='*.pdb' \
  --exclude='*.zip' \
  --exclude='*.ods' \
  ./ "$DEST/"

echo "Synced to: $DEST"
echo "Deployed top level: $(cd "$DEST" && ls -m)"
echo "Enable 'Project Momo Slime Faction' (pmm.slimefaction) in the mod list after Project Momo."
