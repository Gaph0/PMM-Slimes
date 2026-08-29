# Project Momo Slime Faction

RimWorld 1.6 mod adding **wild slimes** for Project Momo: amorphous slime-folk who
wander the warm, wet grasslands. They spawn only in the wild — like wild men — and like
wild men they can be tamed or captured and convinced to join.

## Requirements

- RimWorld **1.6**
- **Biotech** expansion (the slime xenotypes need it)
- **Odyssey** expansion (slimes only spawn in the Odyssey `Grasslands` biome)
- **Harmony** (the slime physiology code is a Harmony-patched assembly)
- **Project Momo** (slime xenotypes carry the Momo gene; slime jelly uses its corruption system)

## Contents

| File | What it defines |
|---|---|
| `About/About.xml` | Mod metadata, Harmony + Biotech + Odyssey + Project Momo dependencies |
| `Defs/XenotypeDefs/Xenotype_Slime.xml` | `PMM_Slime`, `PMM_SlimeRed`, `PMM_SlimeDark`, `PMM_SlimeBubble` xenotypes |
| `Defs/GeneDefs/Gene_SlimeGel.xml` | `PMM_Gene_SlimeGel` custom gene (drives slime physiology) |
| `Defs/PawnKindDefs/PawnKinds_Slime.xml` | Factionless slime pawn kinds: `PMM_SlimeWild` (blue/red/dark mix) and `PMM_SlimeWildBubble` (bubble-only) |
| `Defs/BackstoryDefs/Backstories_Slime.xml` | The single shared slime backstory (childhood + adulthood) |
| `Defs/ThingDefs/ThingDefs_SlimeJelly.xml` | `PMM_SlimeJelly` food item + hidden jelly-oozing hediff |
| `Defs/IncidentDefs/Incidents_Slime.xml` | `PMM_SlimeWandersIn` + `PMM_BubbleSlimeWandersIn` wander-in events |
| `Defs/RulePackDefs/RulePacks_Namers_Slime.xml` | Slime person name generator |
| `Source/SlimeFaction/SlimeMod.cs` | Harmony entry point, DefOfs, gene class, hair/tattoo patches |
| `Source/SlimeFaction/SlimePhysiology.cs` | Filth, bruise-only damage, butchering, wild-man and backstory patches |
| `Source/SlimeFaction/SlimeJelly.cs` | Jelly production comp + jelly ingestion outcome (monsterising) |
| `Source/SlimeFaction/SlimeWandersIn.cs` | The slime wander-in incident workers (grassland + bubble gates) |
| `Source/SlimeFaction/BubbleSlime.cs` | Bubble-slime mechanics: wastepack gizmo, tainted jelly, bond check |
| `Assemblies/PMM_SlimeFaction.dll` | Compiled assembly |

## The ten slime rules

1. **Slimes drop filth, not trash.** A replacement prefix on `Pawn_FilthTracker.Notify_EnteredNewCell`
   keeps the `FilthRate` gate (4x from the gene) but always drops `Filth_Slime` — vanilla's
   66% terrain-filth / 34% `Filth_Trash` branches never run for slimes.
2. **Hair always matches skin; no tattoos.** `Patch_SlimeHairColor` postfixes
   `PawnRenderNode.ColorFor` so slime hair renders in the pawn's own skin colour (blue slime =
   blue hair), never grey from ageing. Four patches on the `Pawn_StyleTracker` tattoo
   setters/getters force `NoTattoo_*` on any slime (generation, styling stations, dev tools,
   and old saves).
3. **One backstory for all slimes.** The slime pawn kinds filter backstory generation to the
   `PMM_SlimeSpawn` category, which holds exactly one childhood (*slime spawnling*) and one
   adulthood (*wandering slime*). A postfix on `GiveAppropriateBioAndNameTo` re-forces both
   slots after generation, so no caller can produce a different story.
4. **All wounds are bruises.** A prefix on `Thing.TakeDamage(ref DamageInfo)` (which `Pawn`
   does not override, so it catches everything before armour and damage workers) rewrites the
   `DamageInfo` in place for slimes: any external-violence damage def becomes `Crush` (flesh
   crush = bruise), and hits aimed at solid parts — bones are the only solid body parts — are
   retargeted to the nearest non-solid ancestor. No broken bones, no cuts.
5. **Same lethal temperatures as a naked human.** No temperature genes, no hypothermia patch:
   slimes get heatstroke and hypothermia at exactly the same thresholds as a baseliner.
6. **Wild men, no factions.** There are no slime factions and no settlements. Slimes enter the
   map only through the wander-in event, factionless. Vanilla hardcodes `WildManUtility.IsWildMan`
   to the WildMan kind; `Patch_SlimeIsWildMan` extends it to the two slime kinds (`PMM_SlimeWild`,
   `PMM_SlimeWildBubble`), which gives taming (Animals → Tame), arrest, wander-off behaviour and
   wild-man labels for free.

   **Staying on the map is a think-tree problem, not a `WildManShouldReachOutsideNow` one.** The
   Humanlike think tree routes pawns to `MainWildManBehaviorCore` via `ThinkNode_ConditionalPawnKind`
   hardcoded to the exact `WildMan` kindDef; every other non-colonist humanlike falls through to the
   fallback `ThinkNode_ConditionalColonist (invert) → JobGiver_ExitMapBest` ("leave the map if you're
   here for no reason"). `ThinkNode_ConditionalPawnKind` matches `pawn.kindDef` exactly (it does NOT
   follow `pawn.def` / the race swap), so `Defs/ThinkTreeDefs/ThinkTrees_Slime.xml` mirrors the vanilla
   wild-man main + idle branches via the `Humanlike_PostMain` insertTag, with one branch per slime
   kindDef. A slime kind omitted there (the bubble kind, before it was added) silently walks off the
   map. As layered hardening, `Patch_SlimeShouldNotReachOutside` (a `WildManShouldReachOutsideNow`
   postfix) plus setting `mindState.WildManEverReachedOutside` on spawn also suppress the wild-man
   "reach outside" job inside `MainWildManBehaviorCore`, so slimes wander until tamed.
7. **Warm, wet grasslands only.** `IncidentWorker_SlimeWandersIn` (subclass of the vanilla
   wild-man worker) reuses the vanilla spawn flow but picks a random slime kind and gates the
   event in `CanFireNowSub`: biome must be Odyssey `Grasslands` (also in the def's
   `allowedBiomes`), coldest-month temperature (`GenTemperature.MinTemperatureAtTile`) at least
   **18 °C** — the old slime faction's settlement-warmth threshold — and rainfall at least
   **1500 mm** (grasslands span 800–2500 mm; the old tropical whitelist biomes sit at 2000 mm+,
   so raise `MinRainfall` in `SlimeWandersIn.cs` to 2000 for strict parity). Slime xenotypes also
   have `factionlessGenerationWeight` 0, so they can never appear as refugees or wanderers
   elsewhere. **Bubble slimes are the exception**: they use their own incident
   (`PMM_BubbleSlimeWandersIn`, a worker subclass overriding the climate gate and the pawn
   kind), which only fires when the map has caves (overhead-mountain roof, sampled on a
   stride) AND the world tile is polluted (`Tile.pollution >= 0.05`) — the dark, filthy
   places they live. The common wander-in's xenotype mix no longer includes bubble.
8. **Butchered into slime jelly, never skin.** A postfix on `Pawn.ButcherProducts` replaces the
   result for slimes with slime jelly (count scales with `MeatAmount` × butcher efficiency).
   Corpses delegate to the inner pawn, so one patch covers both; no meat, no human leather.
   Each slime xenotype yields its own jelly — blue → slime jelly, bubble/red/dark →
   their jellies (market value 8/12/15/20). A single pawn kind `PMM_SlimeWild` spawns the
   colour mix. A custom `PMM_Race_SlimeMomo` race (deep-merged from Human, `meatDef` =
   slime jelly, no leather) makes the living slime's info card agree: race "slime momo", jelly, zero leather.
9. **Slime jelly monsterises women who overeat.** `PMM_SlimeJelly` is an insect-jelly analogue
   (same nutrition, joy, never rots) with **zero** food-poison chance and a custom
   `IngestionOutcomeDoer_SlimeJelly`: each jelly adds 0.08 severity (XML-tunable
   `severityPerJelly`) to Project Momo's `mamono corruption` hediff, imprinted with the
   `PMM_Slime` xenotype. `MomoTransformation.CanEverTransform` gates it to women of corruptible
   age. The corruption decays while she is upright, so casual snacking fades — only sustained
   binging completes the change into a slime. Men are unaffected.
10. **Slimes ooze jelly periodically.** The slime gel gene adds a hidden
    `PMM_Hediff_SlimeJellyOozing` hediff whose comp drops 2–4 slime jelly at the slime's feet
    every day (XML-tunable `intervalDays`/`jellyCount`). The health tracker stops ticking at
    death, so corpses never produce.

## Slime xenotypes

- **Slime (`PMM_Slime`)** — the common blue slime (15 genes): `Skin_Blue`,
  `WoundHealing_SuperFast`, `PsychicAbility_Dull`, `MoveSpeed_Slow`, `Mood_Optimist`,
  `MeleeDamage_Weak`, `Sleepy`, `Robust`, `Pain_Reduced`, `Learning_Slow`,
  `AptitudePoor_Intellectual`, `AptitudePoor_Social`, `RobustDigestion`,
  `ProjectMomo_Momo` (all slimes are female), `PMM_Gene_SlimeGel`.
- **Red Slime (`PMM_SlimeRed`)** — rarer, fiercer: the blue set minus the movement/melee
  penalties (`Skin_DeepRed`; no `MoveSpeed_Slow`/`MeleeDamage_Weak`). Its jelly grants a
  3-hour +10% Moving boost.
- **Dark Slime (`PMM_SlimeDark`)** — rarest: `Skin_Purple`, `PsychicAbility_Enhanced`,
  plus the shared slime package; the only slime with the VPE psycast opt-in. Its jelly
  grants a 3-hour psychic boost (neural heat limit/regen, psyfocus, meditation gain).
- **Bubble Slime (`PMM_SlimeBubble`)** — toxin-eater: the blue set (keeping
  `Skin_SlateGray`) plus `ToxResist_Total`. Player-owned bubble slimes get a "consume
  wastepack" gizmo (destroys a nearby wastepack, burps out a small toxic gas cloud). Its
  jelly always causes food poisoning — except for pawns bonded (Tsugai/animal bond) to a
  bubble slime.

Spawn mix (on `PMM_SlimeWild`'s `xenotypeSet`): slime 100 / red 15 / dark 5 — weights sum
past 100%, so no baseliner is ever rolled. Bubble is excluded: it only spawns via its own
polluted-caves wander-in (rule 7) using the `PMM_SlimeWildBubble` kind.

## Rebuilding the assembly

Build with `csc` (Roslyn), not `mcs` — the source uses `AccessTools.FieldRef` patterns that
`mcs` rejects:

```bash
cd "$HOME/Desktop/Project Momo Slime Faction"
M=~/.steam/steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed
H=~/.steam/steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies
PM="$HOME/Desktop/Project Momo/Assemblies/ProjectMomo.dll"
csc -nologo -target:library \
  -out:"$PWD/Assemblies/PMM_SlimeFaction.dll" \
  -r:"$M/Assembly-CSharp.dll" -r:"$M/UnityEngine.CoreModule.dll" -r:"$M/UnityEngine.IMGUIModule.dll" \
  -r:"$M/netstandard.dll" \
  -r:"$H/0Harmony.dll" -r:"$PM" \
  Source/SlimeFaction/*.cs
```

Build output stays in this workspace (`Assemblies/PMM_SlimeFaction.dll`). Deployment into the
game's `Mods` folder is a separate manual step — never overwrite the DLL while RimWorld is
running, or the loaded assembly's metadata gets corrupted.

## Installation

Copy the `Project Momo Slime Faction` folder into your RimWorld `Mods` folder and enable it in
the mod menu. It loads after Harmony, Biotech, Odyssey and Project Momo automatically.

**Breaking change from the faction version:** saves that contain the old slime confluence
faction or its settlements will throw missing-def errors on load. Start a fresh save.

## Customization notes

- **Xenotype icons** are placeholders reusing the vanilla Impid icon. Add textures at
  `Textures/UI/Icons/Xenotypes/<name>.png` and update `iconPath` in `Xenotype_Slime.xml`.
- **Jelly art** reuses the vanilla insect jelly texture; replace `texPath` in
  `ThingDefs_SlimeJelly.xml` with your own.
- **Spawn climate**: edit `MinColdestMonthTemp` / `MinRainfall` in `SlimeWandersIn.cs` and
  rebuild. To allow other biomes, add them to `allowedBiomes` in `Incidents_Slime.xml` **and**
  the biome check in `ClimateAcceptable`. Bubble gate: `MinTilePollution` / `MinThickRoofSamples`
  in the same file.
- **Monsterising rate**: tune `severityPerJelly` in `ThingDefs_SlimeJelly.xml` (0.08 ≈ 13
  jelly to transform, if eaten faster than the corruption decays).
- **Jelly drip rate**: tune `intervalDays`/`jellyCount` in `ThingDefs_SlimeJelly.xml`.
- **Hair/tattoos/filth/bruises/butchering/wild-man**: each behaviour is one patch class under
  `Source/SlimeFaction/` — delete the class and rebuild to restore vanilla behaviour for that
  rule.
- **Workshop**: this mod's Steam Workshop item id is `3786764216`, stored in
  `About/PublishedFileId` (the uploader writes this file **with no file extension**). Keep it in
  `About/` and never delete it — it's what makes workshop updates land on the same page. Add a
  `Preview.png` (512x512) in `About/` before uploading.
