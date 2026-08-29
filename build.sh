#!/bin/bash
# Builds Assemblies/PMM_SlimeFaction.dll with mcs against RimWorld + workshop mod references.
# Depends on Project Momo being built first (ProjectMomo.dll is referenced).
set -e
cd "$(dirname "$0")"

PM="$HOME/Desktop/Project Momo"
if [ ! -f "$PM/Assemblies/ProjectMomo.dll" ]; then
  echo "ProjectMomo.dll not found — building Project Momo first..."
  "$PM/build.sh"
fi

WS=~/.steam/steam/steamapps/workshop/content/294100
M=~/.steam/steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed
H=$WS/2009463077/Current/Assemblies   # Harmony
VEF=$WS/2023507013/1.6/Assemblies     # Vanilla Expanded Framework

csc -nologo -target:library \
  Source/SlimeFaction/*.cs -out:Assemblies/PMM_SlimeFaction.dll \
  -r:"$M/Assembly-CSharp.dll" -r:"$M/UnityEngine.CoreModule.dll" \
  -r:"$M/UnityEngine.IMGUIModule.dll" -r:"$M/UnityEngine.TextRenderingModule.dll" \
  -r:"$M/netstandard.dll" \
  -r:"$H/0Harmony.dll" -r:"$VEF/VEF.dll" \
  -r:"$PM/Assemblies/ProjectMomo.dll"

echo "Built Assemblies/PMM_SlimeFaction.dll"
