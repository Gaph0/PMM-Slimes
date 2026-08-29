# Parasite Slime / Slime Carrier — Implementation Plan

> **v2 — staged disguise redesign (locked 2026-08-29).** The carrier is no longer
> born with her genes. Lifecycle, shared by the visit event and the item:
>
> 1. **SECRET** — hidden `PMM_Hediff_ParasiteCarrier` marker, **no slime/Momo genes**
>    (Bio tab stays clean — solves the gene-leak problem *without* UI patches). Event:
>    her real colonist backstories are masked with "unknown" placeholders (nureonago
>    trick, `CarrierSlimeBackstories.Mask`). Item: colonist keeps her own stories.
> 2. **COUNTDOWN** — accepting her (event) or eating the item starts the hidden
>    `PMM_Hediff_ParasiteTakeover` (~2 days, `SeverityPerDay 0.5`). She works as a
>    **normal colonist** meanwhile (fertile, no slime traits).
> 3. **TURN** — takeover completes → `ApplyCarrierGenes` adds Momo + slime-gel genes,
>    marker surfaces, **alert letter fires** (`PMM_LetterCarrierTurn`). This is the
>    alert that was missing from the item path.
>
> **Locked decisions (2026-08-29):**
> - Scope: **A and B** — disguise treatment for the event AND the countdown+alert for the item.
> - "Adds the xenotypes" → **adds the genes** (Momo + slime gel), not a xenotype stamp.
> - Backstories: **real revealed** — her pawn kind filters adulthood to the colonist pool
>   (Offworld/Outlander/Tribal/ImperialCommon), childhood full shuffle; the pre-generated
>   real stories are **restored verbatim** on Accept (snapshot on comp, not re-rolled).
> - Reject: **R2** — she leaves quietly (no genes, no reveal, no berserk). Attack: also quiet flee.
> - During the countdown she is a normal colonist (both event-accept and item paths).
>
> Key consequence: physiology guards (`IsCarrier`) only ever bite on a *turned* carrier,
> because each also requires `IsSlime` (gene present) — a secret carrier has the marker
> but no gene, so she fails the gene check and stays vanilla. No guard changes needed.

---

## Original v1 plan (superseded by the redesign above; kept for reference)

Foundation: the Nureonago event (`Source/SlimeFaction/NureonagoVisit.cs`) and the
slime-gel physiology system. The carrier is defined by what she **keeps**: she stays
`Human` race, keeps her xenotype (usually Baseliner), backstories, hair, skin, name and
genes — and gains exactly **one** gene (`PMM_Gene_SlimeGel`) plus **one** marker hediff.

**Key consequence:** no new race, no new xenotype, no new jelly, no new backstories.
Do **not** add entries to `SlimeRaces.RaceFor/JellyFor/XenotypeFor`.

---

## 1. Requirements → mechanics mapping

| # | Requirement | Mechanism |
|---|---|---|
| 1 | Item "Parasite Slime", very rare | New standalone `ThingDef PMM_ParasiteSlime` (see §3) |
| 2 | Consumer keeps backstories, hair, skin colour, all genes | Custom transformation that only **adds**; never swaps xenotype/race/backstories (see §4) |
| 3 | No periodic slime drops; butchering yields human skin + meat only | Carrier guards in `Gene_SlimeGel.PostAdd` and `Patch_SlimeButcherProducts` (see §5) |
| 4 | Only gene added = Slime Gel **+ Momo gene** (amended: carriers ARE Momos) | `pawn.genes.AddGene` for `PMM_Gene_SlimeGel` and `ProjectMomo_Momo` (both endogenes); hair/body snapshot-restored around the Momo gene's PostAdd so her exact hairstyle is kept |
| 5 | Nureonago-like spawn event, no weather dependency | New incident `PMM_CarrierSlimeVisit` cloned from the nureonago pattern minus rain gates (see §6) |
| 6 | Incapable of getting pregnant | `fertilityFactor 0` on the marker hediff stage (vanilla `StatPart_FertilityByHediffs`; confirmed in Biotech defs). Parent's `MomoFertilityPatch` only touches the age curve, so hediff sterility holds for momos too |

---

## 2. What we deliberately do NOT reuse

- **`Hediff_MomoCorruption` / `CompleteTransformation()`** (the jelly pipeline):
  it *replaces* the pawn's xenotype, which would wipe genes/backstories — violates
  requirements 2 and 4. The carrier needs her own transformation path.
- **`PMM_SlimeJellyBase`** as an XML parent for the item: it carries
  `IngestionOutcomeDoer_SlimeJelly` (monsterises into a xenotype slime), inherited
  additively and unremovable. The parasite item is standalone.
- **`PMM_UnknownChildhood/Adulthood`**: the carrier is a real woman with a real past —
  she keeps her vanilla-generated backstories (unlike the nureonago disguise).
- ~~**`ProjectMomo_Momo` gene**: not added~~ — **AMENDED (decision 5): carriers are
  Momos**, so the gene IS added. `Gene_Momo.PostAdd` forces female + Female body and
  swaps non-feminine hairstyles — `MakeCarrier` snapshots `hairDef`/`bodyType` before
  adding genes and restores them after, so her exact hair is kept (requirement 2).
  Side effect of being a Momo: she gains the Mana need (`ProjectMomo_Mana`), her
  melee inflicts tease damage, and she is bond-capable on the momo side.

---

## 3. The item — `PMM_ParasiteSlime`

New file `Defs/ThingDefs/ThingDefs_ParasiteSlime.xml` (one theme per file, per repo
convention), containing the item and both hediffs.

```xml
<ThingDef>
  <defName>PMM_ParasiteSlime</defName>
  <label>parasite slime</label>
  <thingClass>ThingWithComps</thingClass>
  <category>Item</category>
  <stackLimit>1</stackLimit>                <!-- discrete blob, not a stackable food -->
  <statBases><MarketValue>800</MarketValue></statBases>  <!-- D1: tune for "very rare" -->
  <comps><li Class="CompProperties_Forbiddable"/></comps>
  <ingestible>
    <nutrition>0.05</nutrition>
    <outcomeDoers>
      <li Class="PMM_SlimeFaction.IngestionOutcomeDoer_ParasiteSlime"/>
    </outcomeDoers>
  </ingestible>
</ThingDef>
```

**Acquisition (decision D1)** — pick one or more:
- a) `tradeTags` → `ExoticMisc` so exotic-goods traders can stock it;
- b) new `Patches/` dir (release.sh already zips it) injecting a low-count
  `StockGenerator_BuySingleDef` into exotic trader kinds — deterministic rarity;
- c) `thingSetMakerTags` for quest-reward loot tables;
- d) rare byproduct when butchering wild slimes (small extra block in
  `Patch_SlimeButcherProducts`).

Recommendation: a) + b).

---

## 4. The transformation — infestation, not replacement

New file `Source/SlimeFaction/ParasiteSlime.cs`:

```
SlimeCarrierUtility
  IsCarrier(Pawn)            → has PMM_Hediff_ParasiteCarrier
  MakeCarrier(Pawn)          → the ONE place carrier-ification happens
IngestionOutcomeDoer_ParasiteSlime   (gating, then applies infestation hediff)
Hediff_ParasiteInfestation           (optional timed build-up, D6)
Hediff_ParasiteCarrier               (marker; Visible only when "revealed"; scribes flag)
```

**`IngestionOutcomeDoer_ParasiteSlime` gates** (in order):
1. humanlike, female, alive;
2. not already `Gene_SlimeGel.IsSlime(pawn)` (a real slime can't be infested);
3. not already a carrier;
4. pregnant → see D4.

Invalid target (e.g. a man): item is consumed, no effect, message to player (D7).

**`MakeCarrier(pawn, revealed)`** — exact order matters:
1. Snapshot `hairDef` + `bodyType` (the Momo gene's PostAdd may swap a non-feminine
   hairstyle; requirement 2 says she keeps her hair).
2. `pawn.health.AddHediff(PMM_Hediff_ParasiteCarrier)` — so the carrier guard in
   `Gene_SlimeGel.PostAdd` (§5) is already true when the gene lands; `revealed` set
   on the hediff.
3. `pawn.genes.AddGene(ProjectMomo_Momo, xenogene: false)` then
   `pawn.genes.AddGene(PMM_Gene_SlimeGel, xenogene: false)` — plain adds, no
   `RemoveConflictingGenes` (she keeps ALL her genes).
4. Restore the snapshotted `hairDef`/`bodyType` if the Momo gene changed them.
5. Nothing else. No `SetXenotypeDirect`, no race swap, no backstory swap.

**`PMM_Hediff_ParasiteInfestation`** (D6, locked): severity 0.05→1 over ~1.5 days via
`HediffCompProperties_SeverityPerDay`; **invisible** (`Visible => false`, no flavour
stages — the player gets no warning); at max severity it calls `MakeCarrier(revealed:
true)`, removes itself, and sends the **sudden-reveal letter** (`PMM_LetterParasiteCarrierReveal`,
NegativeEvent). Removal inside a 250-tick hash check in `TickInterval` — same safe
self-resolving pattern as the parent's `Hediff_MomoCorruption`.

**`PMM_Hediff_ParasiteCarrier`** (marker):
- `isBad false`, `initialSeverity/maxSeverity 1`, `everCurableByItem false`;
- stage with `fertilityFactor 0` → **sterility (requirement 6)**;
- `hediffClass Hediff_ParasiteCarrier` overrides `Visible` from a scribed `revealed`
  bool: hidden while "disguised", appears in the health tab after the event reveal;
- verify healer-mech-serum can't cure it; if the vanilla cure path ignores
  `everCurableByItem`, add a small prefix patch.

---

## 5. Carrier guards in the existing gel-gene patches

`IsSlime` returns true for carriers (they have the gene), so every patch keyed on it
needs an explicit decision. Guard points:

| Patch (file) | Behaviour for carriers | Change |
|---|---|---|
| `Gene_SlimeGel.PostAdd` (SlimeMod.cs:206) | **No** oozing hediff → no periodic drops (req 3) | `if (SlimeCarrierUtility.IsCarrier(pawn)) return;` before AddHediff |
| `Patch_SlimeButcherProducts` (SlimePhysiology.cs:112) | **Vanilla** butchery → human meat + human leather (req 3). Works because her `pawn.def` stays `Human` (leather intact — `SlimeRaceSetup.NullLeather` only touches the 6 slime races) | early `return` if `IsCarrier(__instance)` |
| `Patch_SlimeHairColor` (SlimeMod.cs:237) | **Keeps her hair colour** (req 2) | skip if `IsCarrier(pawn)` |
| `Patch_SlimeRaceByXenotype` (SlimeMod.cs:164) | **Stays Human**. Should never trigger anyway (gene is added *after* `GeneratePawn` returns, and the postfix's `RaceFor("Baseliner")` default → `PMM_Race_SlimeMomo` would be wrong for her) | belt-and-braces: skip if `IsCarrier(__result)` |
| `Patch_FilthTracker_Slime` | keeps slime filth (D3) | none |
| `Patch_SlimeTakeDamage` | keeps bruise-only damage (D3) | none |
| tattoo patches | keeps no-tattoo (D3) | none |
| `Patch_SlimeBackstory` / `Patch_SlimeIsWildMan` / `Patch_SlimeShouldNotReachOutside` | keyed on `SlimeKinds.IsSlimeKind` (the 4 wild kinds) — carrier kind is **not** added, so she keeps vanilla backstories and is treated as a visitor (req 2) | none — just don't add her kind |

---

## 6. The event — `PMM_CarrierSlimeVisit` (requirement 5)

Clone the nureonago pattern, minus weather. Deltas:

**IncidentDef** (`Defs/IncidentDefs/Incidents_Slime.xml`):
`workerClass PMM_SlimeFaction.IncidentWorker_CarrierSlimeVisit`, `baseChance` ~0.4 (D8),
`Misc` / `Map_PlayerHome` / `NeutralEvent`, letter text from keyed strings.

**PawnKindDef** (`Defs/PawnKindDefs/PawnKinds_Slime.xml`): `PMM_CarrierSlimeRefugee` —
copy of `PMM_NureonagoRefugee` (Human, `fixedGender Female`, Baseliner xenotypeSet, no
gear). She generates as a *real* woman: vanilla backstories, hair, skin, name. **No**
`SetUnknownBackstories` call.

**Worker** — same flow as `IncidentWorker_NureonagoVisit.TryExecuteWorker`:
edge cell → `GeneratePawn` → de-guest trick (`SetFaction(null)` **then** clear
`hostFactionInt` via the existing FieldRef) → `SlimeCarrierUtility.MakeCarrier(pawn)`
(post-generation, so the race-swap postfix never sees her gel gene) → `GenSpawn.Spawn`
→ attach visitor comp → arrival letter.

**Gates** (`CanFireNowSub`):
- ~~IsRaining~~ — **removed** (req 5);
- `HasBondableMan` — keep (D2; the parasite seeks men, same as the nureonago);
- `HasCarrier` clamp — scan for kind `PMM_CarrierSlimeRefugee` **or** any pawn with
  `PMM_Hediff_ParasiteCarrier` (she has no unique xenotype, so the hediff is the scan key).

**Visitor brain** — two approaches:
- **(Recommended)** Light generalisation of `NureonagoVisitorComp` /
  `JobGiver_NureonagoWait`: the rain-check becomes a virtual/delegate; the carrier
  version leaves after a scribed `departTick` (~18 in-game hours if ignored, D8).
  Mirrors the proven `IncidentWorker_SlimeWandersIn` subclass pattern.
- Lower-risk fallback: copy `NureonagoVisit.cs` → `CarrierSlimeVisit.cs` and trim.

**Talk flow** — same shape as the nureonago, with its own classes in
`CarrierSlimeVisit.cs` (float-menu postfix, `JobDriver_TalkToCarrierSlime`,
`CarrierSlimeDialogue`):
- **Accept** → reveal: letter + `revealed = true` on the hediff (no race/backstory
  swap — nothing to swap) → `SetFaction(OfPlayer)`.
- **Reject** → reveal, then the **same level check as the nureonago** (D5, locked:
  carriers are Momos) — `IsekaiCompat.GetLevel` carrier > talker → `EssenceBerserk`;
  otherwise she exits the map.
- **Attacked** → disguise breaks (own postfix on `Thing.TakeDamage`) → reveal +
  `EssenceBerserk`, same as `NureonagoVisitorComp.Notify_Attacked`.

**Think tree** (`Defs/ThinkTreeDefs/ThinkTrees_Slime.xml`): add a
`PMM_CarrierSlimeRefugee` branch to `PMM_SlimeWildIdle` with the wait JobGiver.
Do **not** touch `PMM_SlimeWildBehavior`.

**Dev-spawn path**: extend the kind-specific block in `Patch_SlimeRaceByXenotype`
(currently nureonago-only, SlimeMod.cs:155) to also handle `PMM_CarrierSlimeRefugee`:
attach comp, clear host faction, and `MakeCarrier`.

---

## 7. Strings & labels

All keyed strings go in `Languages/English/Keyed/SlimePsycasts.xml` (repo convention):
arrival letter label/text, reveal letter, dialogue title/body/accept/reject, "no effect"
ingestion messages, disguise-broken message. Item/hediff labels + descriptions live in
the defs themselves.

---

## 8. Work breakdown

**Phase 1 — core item & transformation (testable without the event)**
1. `Defs/ThingDefs/ThingDefs_ParasiteSlime.xml`: item + both hediffs.
2. `Source/SlimeFaction/ParasiteSlime.cs`: utility, doer, hediff classes.
3. Guards in `SlimeMod.cs` (PostAdd, hair, race-swap) and `SlimePhysiology.cs` (butcher).
4. `SlimeDefOf` entries: item, 2 hediffs.
5. `./build.sh`; dev-test: spawn item → feed to female colonist → verify §9 checklist.

**Phase 2 — the visit event**
6. PawnKindDef, IncidentDef, think-tree branch.
7. `CarrierSlimeVisit.cs` (or generalised shared visitor code).
8. Dev-spawn block in `Patch_SlimeRaceByXenotype`.
9. Keyed strings.

**Phase 3 — acquisition & polish**
10. Trader/quest loot hooks (D1), balance pass on chance/value/duration.
11. CHANGELOG + README.

---

## 9. Test checklist

- [ ] Female colonist eats item → (infestation →) carrier: marker hediff present, gel
      gene present as **endogene**, xenotype unchanged, backstories unchanged,
      hair/skin unchanged.
- [ ] Bio tab shows Fertility 0 (hediff factor). No new conceptions possible.
- [ ] No `PMM_Hediff_SlimeJellyOozing` on her; nothing drops over time.
- [ ] Butcher her corpse → `Meat_Human` + `Leather_Human` only.
- [ ] Man eats item → consumed, no effect, message.
- [ ] Existing slime eats item → no effect, message.
- [ ] Incident fires under **clear** weather (dev "execute incident"); nureonago still
      requires rain.
- [ ] Talk → accept (joins, hediff revealed) / reject (leaves) / attack (reveals, flees)
      / ignored (departs on timer).
- [ ] Save/load mid-visit and post-recruitment (comp + hediff scribing).
- [ ] Dev-spawn `PMM_CarrierSlimeRefugee` → arrives carrier-ified, stays put.
- [ ] Second visit doesn't fire while a carrier is on the map.

---

## 10. Decisions to confirm before implementation

| # | Question | Decision (locked 2026-08-29) |
|---|---|---|
| D1 | Item acquisition | ✅ ExoticMisc tag + `StockGenerator_SingleDef` patch on `Caravan_Outlander_Exotic`/`Orbital_Exotic`; MV 800 |
| D2 | Keep the bondable-man gate for the event? | ✅ Keep |
| D3 | Gel-gene side effects carriers keep | ✅ Keep: slime filth, bruise-only damage, no tattoos. Drop: oozing, jelly butchery, hair colour |
| D4 | Pregnant woman consumes the item? | ✅ Allow; existing pregnancy continues, no future conceptions |
| D5 | Reject/attack outcomes | ✅ **Berserk** (nureonago flow incl. level check) — carriers ARE Momos, so `ProjectMomo_Momo` is added too (amends note 4: two genes, not one) |
| D6 | Transformation timing | ✅ ~1.5-day infestation hediff, **invisible**, then a **sudden reveal** (letter + carrier hediff becomes visible) |
| D7 | Male/invalid consumer | ✅ Item consumed, no effect, message |
| D8 | Event baseChance / ignored-visit duration | ✅ 0.4 / ~18 in-game hours (45000 ticks) |
