# Project Momo Slime Faction

RimWorld 1.6 mod adding **wild slimes** for Project Momo: amorphous slime-folk who wander
in from the wilds — warm grasslands, polluted caverns, the deep underground, and the sea.
They spawn only in the wild, like wild men — and like wild men they can be tamed or
captured and convinced to join. Some slimes don't wait to be found: disguised as ordinary
women, they walk up to the colony on their own.

## Requirements

Hard dependencies (load order handled automatically):

- **Harmony** (the slime physiology code is a Harmony-patched assembly)
- **Biotech** (the slime xenotypes and genes)
- **Odyssey** (the `Grasslands` biome, coastal spawns and fishing)
- **Project Momo** (`PMM.Core` — slime xenotypes carry the Momo gene; slime jelly, the
  parasite slime and the dark slime's psycasts all feed its corruption/essence systems)

Optional integrations, detected at runtime:

| Mod | What it unlocks |
|---|---|
| Vanilla Psycasts Expanded | Dark slimes spawn as psycasters of the Slime Core path |
| ISEKAI RPG Leveling | Wild slimes spawn capped at mob rank E/F; nureonago rejection checks levels |
| Anomaly | Taisui social interactions can become "strange chats" |

## Contents

| File | What it defines |
|---|---|
| `About/About.xml` | Mod metadata, Harmony + Biotech + Odyssey + Project Momo dependencies |
| `Defs/XenotypeDefs/Xenotype_Slime.xml` | The seven slime xenotypes (blue, red, dark, bubble, taisui, sea, nureonago) |
| `Defs/GeneDefs/Gene_SlimeGel.xml` | `PMM_Gene_SlimeGel` (slime physiology), sea-foam/wet-pale skin genes, `PMM_Gene_Fastidious` |
| `Defs/PawnKindDefs/PawnKinds_Slime.xml` | Four wild slime kinds + the two disguised-visitor kinds |
| `Defs/BackstoryDefs/Backstories_Slime.xml` | Slime-spawn backstories (+ sea variant) and the "unknown" disguise backstories |
| `Defs/ThingDefs/Race_SlimeMomo.xml` | The slime momo races (Human deep-merge; jelly meat, zero leather) |
| `Defs/ThingDefs/ThingDefs_SlimeJelly.xml` | The eight slime jellies, their effect hediffs, jelly-oozing and sea-paralytic hediffs |
| `Defs/ThingDefs/ThingDefs_ParasiteSlime.xml` | Parasite slime item + takeover/carrier hediffs |
| `Defs/IncidentDefs/Incidents_Slime.xml` | Four wander-in events + the nureonago/carrier visit events |
| `Defs/ThinkTreeDefs/ThinkTrees_Slime.xml` | Wild-man think-tree branches for the wild slime kinds |
| `Defs/JobDefs/Jobs_Slime.xml` | The "talk to the stranger/traveller" jobs |
| `Defs/PsycastDefs/` | Slime Core psycast path, five abilities, their hediffs, the slime web |
| `Defs/ThoughtDefs/Thoughts_Slime.xml` | Mochi warmth mood thought |
| `Defs/RulePackDefs/RulePacks_Namers_Slime.xml` | Slime person name generator |
| `Source/SlimeFaction/` | The C# assembly source (physiology, events, jelly, visitors, psycasts, gizmos) |
| `Assemblies/PMM_SlimeFaction.dll` | Compiled assembly |

## The slime rules

Physiology and spawning rules enforced by the `PMM_Gene_SlimeGel` gene class and the
Harmony patches (roughly one patch class per rule under `Source/SlimeFaction/`):

1. **Slimes drop filth, not trash.** A replacement prefix on `Pawn_FilthTracker.Notify_EnteredNewCell`
   keeps the `FilthRate` gate (4x from the gene) but always drops `Filth_Slime` — vanilla's
   66% terrain-filth / 34% `Filth_Trash` branches never run for slimes.
2. **Hair always matches skin; no tattoos.** `Patch_SlimeHairColor` postfixes
   `PawnRenderNode.ColorFor` so slime hair renders in the pawn's own skin colour (blue slime =
   blue hair), never grey from ageing. Four patches on the `Pawn_StyleTracker` tattoo
   setters/getters force `NoTattoo_*` on any slime (generation, styling stations, dev tools,
   and old saves). Slime carriers keep their natural hair colour — nothing about them reads
   as a monster.
3. **One backstory for all slimes.** The slime pawn kinds filter backstory generation to the
   `PMM_SlimeSpawn` category, which holds exactly one childhood (*slime spawnling*) and one
   adulthood (*wandering slime*) — sea slimes instead draw the fishing-flavoured *drifting
   sea slime* adulthood (`PMM_SlimeSeaSpawn`, Animals +4). A postfix on
   `GiveAppropriateBioAndNameTo` re-forces both slots after generation, so no caller can
   produce a different story. The two disguised visitors wear Anomaly-style *unknown*
   backstories (`PMM_Unknown*`) until the colony learns who they really are.
4. **All wounds are bruises.** A prefix on `Thing.TakeDamage(ref DamageInfo)` (which `Pawn`
   does not override, so it catches everything before armour and damage workers) rewrites the
   `DamageInfo` in place for slimes: any external-violence damage def becomes `Blunt`, and
   hits aimed at solid parts — bones are the only solid body parts — are retargeted to the
   nearest non-solid ancestor. No broken bones, no cuts. (`Blunt`, not `Crush`:
   `HealthUtility.GetHediffDefFromDamage` prefers a def's `hediffSkin` on skin-covered parts,
   and vanilla `Crush`'s `hediffSkin` is `Cut` — converting to `Crush` made every flesh
   wound a cut. `Blunt`'s `hediffSkin` is `Bruise`.) The patch keys off the gel gene, so
   slime carriers bruise the same way.
5. **Same lethal temperatures as a naked human.** No temperature genes, no hypothermia patch:
   slimes get heatstroke and hypothermia at exactly the same thresholds as a baseliner.
6. **Wild men, no factions.** There are no slime factions and no settlements. Slimes enter the
   map only through their own events, factionless. Vanilla hardcodes `WildManUtility.IsWildMan`
   to the WildMan kind; `Patch_SlimeIsWildMan` extends it to the four wild slime kinds
   (`PMM_SlimeWild`, `PMM_SlimeWildBubble`, `PMM_SlimeWildTaisui`, `PMM_SlimeWildSea`), which
   gives taming (Animals → Tame), arrest, wander-off behaviour and wild-man labels for free.

   **Staying on the map is a think-tree problem, not a `WildManShouldReachOutsideNow` one.** The
   Humanlike think tree routes pawns to `MainWildManBehaviorCore` via `ThinkNode_ConditionalPawnKind`
   hardcoded to the exact `WildMan` kindDef; every other non-colonist humanlike falls through to the
   fallback `ThinkNode_ConditionalColonist (invert) → JobGiver_ExitMapBest` ("leave the map if you're
   here for no reason"). `ThinkNode_ConditionalPawnKind` matches `pawn.kindDef` exactly (it does NOT
   follow `pawn.def` / the race swap), so `Defs/ThinkTreeDefs/ThinkTrees_Slime.xml` mirrors the vanilla
   wild-man main + idle branches via the `Humanlike_PostMain` insertTag, with one branch per slime
   kindDef — every wild slime kind must be listed there or it silently walks off the map. (The two
   disguised-visitor kinds deliberately are NOT wild men: they wait at the colony's edge under
   their own visitor comps instead.) As layered hardening, `Patch_SlimeShouldNotReachOutside`
   (a `WildManShouldReachOutsideNow` postfix) plus setting `mindState.WildManEverReachedOutside`
   on spawn also suppress the wild-man "reach outside" job inside `MainWildManBehaviorCore`,
   so slimes wander until tamed.
7. **Every slime spawns where it belongs.** Four wander-in incidents, each subclassing the
   vanilla wild-man worker (reusing its spawn flow but skipping the 16–26 °C season gate and
   the former-faction requirement), each spawning its own kind, factionless, at a map edge;
   all are blocked during toxic fallout and noxious haze:

   | Incident | Pawn kind | Base chance | Gate |
   |---|---|---|---|
   | `PMM_SlimeWandersIn` | `PMM_SlimeWild` (blue 100 / red 15 / dark 5) | 1.0 | Odyssey `Grasslands` biome (also the def's `allowedBiomes`), coldest month (`GenTemperature.MinTemperatureAtTile`) ≥ **18 °C**, rainfall ≥ **1500 mm** |
   | `PMM_BubbleSlimeWandersIn` | `PMM_SlimeWildBubble` | 1.6 | World tile pollution ≥ 5% **and** caves on the tile |
   | `PMM_TaisuiWandersIn` | `PMM_SlimeWildTaisui` | 0.05 | Caves on the tile — no biome/climate gate; about as rare as a dark slime |
   | `PMM_SeaSlimeWandersIn` | `PMM_SlimeWildSea` | 1.0 | Coastal world tile bordering an ocean |

   The cave gate reads the tile's cave TileMutator via `World.HasCaves` (an earlier roof-grid
   sampler false-positived on ordinary mountain rock). Slime xenotypes also have
   `factionlessGenerationWeight` 0, so they can never appear as refugees or wanderers
   elsewhere. Sea slimes have a second way in — see "Xenotype mechanics" below.
8. **Butchered into slime jelly, never skin.** A postfix on `Pawn.ButcherProducts` replaces the
   result for slimes with slime jelly (`MeatAmount` 20 × butcher efficiency, rounded; corpses
   delegate to the inner pawn, so one patch covers both). Each slime race is deep-merged from
   Human with the matching jelly as `specificMeatDef` and no leather, so the living slime's
   info card agrees too: blue → blue slime jelly, red/dark/bubble/taisui/sea → their own
   jellies, nureonago → mochi jelly. **Slime carriers are the exception**: they butcher as
   ordinary humans (skin and meat, no jelly).
9. **Slime jelly monsterises women who overeat.** Every jelly is an insect-jelly analogue
   (same nutrition, joy, never rots) with **zero** random food-poison chance and a custom
   `IngestionOutcomeDoer_SlimeJelly`: each jelly adds 0.08 severity (XML-tunable
   `severityPerJelly`, ≈13 jelly in a short sitting) to Project Momo's `mamono corruption`
   hediff, imprinted with the xenotype matching the jelly eaten (taisui jelly → taisui,
   mochi → nureonago, blue/generic → slime, …). `MomoTransformation.CanEverTransform` gates
   it to women of corruptible age; men and monsters are unaffected. The corruption decays
   while she is upright, so casual snacking fades — only sustained binging completes the
   change, which then fires immediately.
10. **Slimes ooze jelly periodically.** The slime gel gene adds a hidden
    `PMM_Hediff_SlimeJellyOozing` hediff whose comp drops 2–4 slime jelly at the slime's feet
    every day (XML-tunable `intervalDays`/`jellyCount`; on unwalkable terrain the drop lands
    on a reachable cell nearby). The health tracker stops ticking at death, so corpses never
    produce — and slime carriers never ooze at all.
11. **Slimes don't mind being wet.** Slimes never gain the `SoakingWet` thought, and any
    existing memories of it are scrubbed on the next mind-state tick — a semi-liquid body
    is always wet anyway.

## Slime xenotypes

Seven xenotypes — all inheritable, all female (`ProjectMomo_Momo`), all gelatinous
(`PMM_Gene_SlimeGel`), none ever spawning as ordinary refugees or wanderers
(`factionlessGenerationWeight` 0):

- **Slime (`PMM_Slime`)** — the common blue slime (15 genes): `Skin_Blue`,
  `WoundHealing_SuperFast`, `PsychicAbility_Dull`, `MoveSpeed_Slow`, `Mood_Optimist`,
  `MeleeDamage_Weak`, `Sleepy`, `Robust`, `Pain_Reduced`, `Learning_Slow`,
  `AptitudePoor_Intellectual`, `AptitudePoor_Social`, `RobustDigestion`, plus the Momo and
  slime-gel genes. A deliberately weak, cheap-to-keep mix.
- **Red Slime (`PMM_SlimeRed`)** — rarer, fiercer: the blue set minus the movement/melee
  penalties (`Skin_DeepRed`; no `MoveSpeed_Slow`/`MeleeDamage_Weak`).
- **Dark Slime (`PMM_SlimeDark`)** — rarest of the grassland mix: `Skin_Purple`,
  `PsychicAbility_Enhanced` and a trimmed gene set; the only slime with the VPE psycast
  opt-in (Slime Core path, 2–3 initial abilities, 2–4 stat upgrade points).
- **Bubble Slime (`PMM_SlimeBubble`)** — sewer/cave toxin-eater: the blue set with
  `Skin_SlateGray` plus `ToxResist_Total`.
- **Taisui (`PMM_SlimeTaisui`)** — cave-dweller of legendary intellect: the blue set with
  `Skin_PaleYellow`, `AptitudeRemarkable_Intellectual`, `AptitudeTerrible_Social`, and no
  `Learning_Slow`.
- **Sea Slime (`PMM_SlimeSea`)** — coastal drifter: the blue set with pale sea-foam skin;
  her pawn kind adds a 2.5× Water terrain move factor, and her backstory carries
  Animals +4 (which drives the Odyssey fishing stats).
- **Nureonago (`PMM_SlimeNureonago`)** — the devoted-housewife slime: the blue set with
  wet-pale skin, the `KindInstinct` gene and `PMM_Gene_Fastidious` (80% less filth, 50%
  faster cleaning). Only enters via her rain visit event.

Spawn mix (on `PMM_SlimeWild`'s `xenotypeSet`): slime 100 / red 15 / dark 5 — weights sum
past 100%, so no baseliner is ever rolled. Bubble, taisui, sea and nureonago are excluded:
each spawns only from its own gated incident.

## Slime jelly

Every slime jelly is an insect-jelly analogue — 0.05 nutrition, 8% gluttonous joy, never
rots, **zero** random food-poison chance — and every one monsterises women who overeat it
(rule 9), imprinting the xenotype it came from. Each variant adds its own effect:

| Jelly | Value | Effect on eating | Transforms women into |
|---|---|---|---|
| slime jelly (generic) | 8 | — | slime |
| blue slime jelly | 8 | — | slime |
| bubble slime jelly | 12 | **Guaranteed food poisoning** — unless bonded (tsugai or animal) to a bubble slime; flushes all toxic buildup; +30% toxic resistance (3h) | bubble slime |
| red slime jelly | 15 | +10% Moving (3h) | red slime |
| dark slime jelly | 20 | +5 neural heat limit, +0.5 heat regen, +0.05 meditation focus gain (3h) | dark slime |
| taisui jelly | 25 | +0.5 research speed and global learning (6h) — the legendary elixir | taisui |
| sea slime jelly | 12 | +10% global work speed, +5% consciousness (3h) | sea slime |
| mochi slime jelly | 12 | +6 mood (8h) | nureonago |

Butchering a slime yields about 20 of her own jelly (rule 8); a living slime sheds 2–4 a
day (rule 10). The generic `PMM_SlimeJelly` is kept for save compatibility and doubles as
the race info-card meat and the fallback for anything unrecognized.

## Xenotype mechanics

### Bubble slime: wastepack eater

Player-owned bubble slimes (Biotech active) get a **Consume wastepack** gizmo: she devours
an *entire* wastepack stack within 20 cells and burps out a small toxic gas cloud (2 gas
per pack), on a 12-hour cooldown. Her tainted jelly is safe only for pawns bonded to a
bubble slime — everyone else gets food poisoning (but even they get their toxic buildup
flushed and a few hours of partial toxic resistance).

### Sea slime: paralytic tentacles and fishing

- Player-owned sea slimes get a **Paralytic tentacles** gizmo: injects paralytic poison
  into any pawn within 6 cells — Moving ×0.25 for 1 in-game hour (re-using it tops the
  dose back up), on a 12-hour cooldown.
- With Odyssey active on a coastal map, every completed fishing catch by a colonist has a
  **6%** chance to hook a wild sea slime instead: she surfaces factionless in the fished
  water cell, tameable like any wild slime.

### Taisui: strange chats

With Anomaly active, roughly 1 in 10 of a taisui's social interactions becomes an Anomaly
"strange chat" (a postfix on `Pawn_StoryTracker.IsDisturbing`; all effects come from
Anomaly's own DisturbingChat). Without Anomaly the patch is inert.

### Dark slime: the Path of the Slime Core (VPE)

With Vanilla Psycasts Expanded loaded, spawned dark slimes unlock the mod's custom
psycaster path (`PMM_Path_SlimeCore` — the only path she can unlock).
The whole tree feeds Project Momo's economies: tease damage erodes willpower, drained
essence refills the caster's Mana, and female victims accrue **hidden mamono corruption**
imprinted with the dark slime xenotype (requires Project Momo's Corruption setting):

| Ability | Tier | Effect |
|---|---|---|
| Sticky Slime Bomb | 1 | AoE burst (radius 5): 0.10 tease + 5% corruption; victims left slime-coated (−20% move, 10s) |
| Slime Grope | 1 | Single target: 0.20 tease, sips 0.10 essence into her mana, 10% corruption |
| Draining Slime Splash | 2 | Lays a living slime patch for 15s: each second, 0.005 tease + 0.01 essence + 0.5% corruption to anyone standing in it |
| Amoeba Hold | 2 | Engulfs a victim: both rooted 5s; 0.10 tease, 0.20 essence, 10% corruption |
| Pleasure Ascension | 3 | Only on slime-coated victims: bound 20s under 0.40 tease waves every 10s — then **+75% corruption** on women, one infusion short of becoming a slime |

Tier 2 requires its tier-1 prerequisite; tier 3 requires both tier-2 abilities.
"Slime-coated" means slowed by the bomb, caught in the hold, or standing in the splash.
Costs (psyfocus 0.10–0.30, entropy 10–30, cooldowns 10–60s) live in
`Psycasts_SlimeCore.xml`.

## Disguised visitors

### The nureonago — a strange woman in the rain

`PMM_NureonagoVisit` fires only while it rains (`Rain`, `RainyThunderstorm`, or Odyssey's
`TorrentialRain`), only while the colony has at least one bondable man, and only if no
nureonago is already on the map (base chance 0.8, rarer in practice than it looks). An
ordinary-looking woman with *unknown* backstories walks in and waits at the colony's
edge — and leaves when the rain stops.

- **Talk to her (right-click) and accept**: the disguise melts away (nureonago genes,
  wet-pale skin, slime race) and she joins the colony.
- **Reject her**: she reveals herself — and if her Isekai level outlevels the pawn who
  spurned her, she flies into an essence berserk; otherwise she slips away.
- **Attack her**: the disguise breaks the same way — reveal, then berserk.

### The slime carrier — a travelling woman

`PMM_CarrierSlimeVisit` needs no rain — a parasite slime passes as human anywhere. It
fires only while the colony has a bondable man and no other carrier on the map (base
chance 0.4). She is a real, once-ordinary woman: vanilla name, looks and colonist-pool
backstories (masked as *unknown* until accepted), dressed like any down-on-her-luck
traveller. She waits about 18 in-game hours before moving on.

- **Accept**: she joins as a normal colonist and her true backstories are restored —
  while the parasite already inside her begins its silent ~2-day takeover. Nothing is
  revealed until the turn.
- **Reject**: she quietly leaves, secret intact.
- **Attack her**: the disguise breaks and she flees the map (she has no monster genes yet).

## Parasite slime

`PMM_ParasiteSlime` is a rare single-blob item (800 silver; sporadic exotic-goods trader
stock via the `ExoticMisc` trade tag — never guaranteed). A woman who eats it becomes
infested; anyone else feels nothing.

The takeover (`PMM_Hediff_ParasiteTakeover`) is hidden, uncurable and climbs over ~2 days.
At full severity the **turn** fires: she gains the Momo and slime-gel genes as *endogenes*
— keeping her race, xenotype, name, backstories, hair, skin and every gene she already had
— and the colony gets a turn alert letter.

The resulting **slime carrier**:

- passes as the woman she was — the carrier marker is invisible until the turn;
- is permanently sterile (the slime has fused with her womb);
- bruises like a slime and sheds slime filth, but keeps her natural hair colour;
- never oozes jelly, and butchers as an ordinary human (skin and meat, no jelly).

## Isekai mob ranks

With ISEKAI RPG Leveling loaded, wild slimes spawn capped at mob rank **E** (25%) or **F**
(75%), level ≤ 10 — slimes sit at the bottom of the Mamono food chain. The cap is applied
when the rank is rolled, not as an override: tamed slimes still gain XP and rank up past E
through play. It keys off the pawn kind or the gel gene, so women transformed into slimes
(jelly binge, parasite takeover) are capped the same way when their rank is recalculated.

## Rebuilding the assembly

Run `./build.sh` from anywhere. It compiles `Source/SlimeFaction/*.cs` with Roslyn `csc`
against RimWorld, Harmony, Vanilla Expanded Framework and Isekai Leveling references plus
`ProjectMomo.dll`, writing `Assemblies/PMM_SlimeFaction.dll` — and builds Project Momo
first if its assembly is missing. (The script is also wired into the pre-push hook chain.)

Build output stays in this workspace. Deployment into the game's `Mods` folder is a
separate manual step — never overwrite the DLL while RimWorld is running, or the loaded
assembly's metadata gets corrupted.

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
- **Jelly art** and the **parasite slime** reuse the vanilla insect jelly texture; replace
  `texPath` in `ThingDefs_SlimeJelly.xml` / `ThingDefs_ParasiteSlime.xml` with your own.
- **Spawn gates**: edit the checks in each incident worker in `SlimeWandersIn.cs`
  (`MinColdestMonthTemp` / `MinRainfall` for the common slime, pollution for bubble) and
  `allowedBiomes` in `Incidents_Slime.xml`, then rebuild. Cave gates use `World.HasCaves`;
  the sea gate reads the world tile's coastal flag.
- **Monsterising rate**: tune `severityPerJelly` in `ThingDefs_SlimeJelly.xml` (0.08 ≈ 13
  jelly to transform, if eaten faster than the corruption decays).
- **Jelly drip rate**: tune `intervalDays`/`jellyCount` in `ThingDefs_SlimeJelly.xml`.
- **Gizmos and psycasts**: wastepack/tentacle ranges and cooldowns are comp defaults in
  `BubbleSlime.cs` (20 cells, 12h) and `SeaSlimeParalytic.cs` (6 cells, 12h); psycast costs
  in `Psycasts_SlimeCore.xml`, effect numbers in `SlimePsycasts.cs`.
- **Visit events**: gates, patience timers and dialog outcomes in `NureonagoVisit.cs` and
  `CarrierSlimeVisit.cs`; takeover pacing in `ThingDefs_ParasiteSlime.xml`
  (`severityPerDay` 0.5 ≈ 2 days).
- **Mob-rank cap**: rank weights and the level ceiling in `SlimeMobRank.cs`.
- **Hair/tattoos/filth/bruises/butchering/wild-man**: each behaviour is one patch class under
  `Source/SlimeFaction/` — delete the class and rebuild to restore vanilla behaviour for that
  rule.
- **Workshop**: this mod's Steam Workshop item id is `3786764216`, stored in
  `About/PublishedFileId` (the uploader writes this file **with no file extension**). Keep it in
  `About/` and never delete it — it's what makes workshop updates land on the same page. Add a
  `Preview.png` (512x512) in `About/` before uploading.
