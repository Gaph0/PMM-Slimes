#!/bin/bash
# Builds Assemblies/PMM_SlimeFaction.dll via dotnet (PMM.SlimeFaction.csproj).
# The core ProjectMamono.csproj is built automatically through the ProjectReference.
set -e
cd "$(dirname "$0")"

dotnet build PMM.SlimeFaction.csproj -v:m

# The core build overwrites 'Project Mamono/About/About.xml' with a generated
# minimal version. Restore the hand-written one so sync.sh deploys correctly.
git -C "$HOME/Desktop/Project Mamono" checkout -- About/About.xml 2>/dev/null || true

echo "Built Assemblies/PMM_SlimeFaction.dll"
