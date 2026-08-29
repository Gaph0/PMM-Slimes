using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// A slime wanders in. Modelled on the vanilla wild-man incident, but spawns a random
    /// slime pawn kind (with the slime xenotype mix) instead of a WildMan, and only fires
    /// in warm, wet grasslands — the same climate rule the old slime faction's settlement
    /// code used: coldest-month temperature of at least ~18 °C and plentiful rainfall.
    /// Wild slimes are tamed like wild men (see Patch_SlimeIsWildMan).
    /// </summary>
    public class IncidentWorker_SlimeWandersIn : IncidentWorker_WildManWandersIn
    {
        /// <summary>Only grassland tiles are valid homes for slimes.</summary>
        private const string GrasslandBiome = "Grasslands";

        /// <summary>
        /// Coldest-month minimum temperature in °C, matching the old slime faction's
        /// minSettlementTemperatureChanceCurve (weight jumped from ~0 to 1 at 18 °C).
        /// </summary>
        private const float MinColdestMonthTemp = 18f;

        /// <summary>
        /// Minimum rainfall in mm. Grasslands span 800–2500 mm; the old whitelisted
        /// tropical biomes sit at 2000 mm+, so 1500 selects the wetter grasslands.
        /// Raise to 2000 for strict parity with the colony spawning rule.
        /// </summary>
        private const float MinRainfall = 1500f;

        /// <summary>The pawn kind this incident spawns. The bubble and taisui variants override this.</summary>
        protected virtual PawnKindDef PawnKindToSpawn => DefDatabase<PawnKindDef>.GetNamed("PMM_SlimeWild");

        /// <summary>Roof sampling stride: every 8th cell in x and z (~1.5% of the map).</summary>
        protected const int RoofSampleStride = 8;

        /// <summary>
        /// Thick-roof sample hits needed to count as "has caves". 8 hits at stride 8 is
        /// ~500 overhead-mountain cells - a modest cave system, not a full mountain.
        /// </summary>
        protected const int MinThickRoofSamples = 8;

        /// <summary>
        /// Cheap cave check: sample the roof grid on a stride and count overhead
        /// mountain (thick rock roof). A cave system has hundreds of such cells, so a
        /// coarse sample finds it without scanning the whole map every storyteller tick.
        /// Shared by the bubble (caves + pollution) and taisui (caves only) wander-ins.
        /// </summary>
        protected static bool HasCaves(Map map)
        {
            int hits = 0;
            RoofGrid roofGrid = map.roofGrid;
            for (int x = 0; x < map.Size.x; x += RoofSampleStride)
            {
                for (int z = 0; z < map.Size.z; z += RoofSampleStride)
                {
                    if (roofGrid.RoofAt(new IntVec3(x, 0, z)) == RoofDefOf.RoofRockThick &&
                        ++hits >= MinThickRoofSamples)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Vanilla checks, plus the climate gate (grassland / warm / humid by default).</summary>
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            // Skip the vanilla SeasonAcceptableFor(Human) check (it gates on the CURRENT
            // seasonal temperature being within a human's comfy range 16-26C, so hot grasslands
            // can never spawn wild men in warm seasons). Slimes aren't migrating for comfort,
            // so we run the rest of the base gates but substitute our own climate rule below.
            if (!BaseCanFireNowSub(parms))
            {
                return false; // BaseCanFireNowSub logs the specific failing gate
            }

            if (!(parms.target is Map map))
            {
                Log.Message("[PMM_Slime] wander-in blocked: target is not a map");
                return false;
            }
            if (!ClimateAcceptable(map))
            {
                return false; // ClimateAcceptable logs the specific failing gate
            }
            if (!TryFindEntryCell(map, out _))
            {
                Log.Message("[PMM_Slime] wander-in blocked: no edge cell can reach the colony");
                return false;
            }
            return true;
        }

        /// <summary>
        /// The biome/climate gate: warm, wet grasslands for common slimes. The bubble
        /// variant overrides this with its caves-and-pollution rule.
        /// </summary>
        protected virtual bool ClimateAcceptable(Map map)
        {
            RimWorld.Planet.Tile tile = Find.WorldGrid[map.Tile];
            if (tile?.PrimaryBiome == null || tile.PrimaryBiome.defName != GrasslandBiome)
            {
                Log.Message($"[PMM_Slime] wander-in blocked: biome is {tile?.PrimaryBiome?.defName ?? "null"}, need {GrasslandBiome}");
                return false;
            }
            if (GenTemperature.MinTemperatureAtTile(map.Tile) < MinColdestMonthTemp)
            {
                Log.Message($"[PMM_Slime] wander-in blocked: coldest month {GenTemperature.MinTemperatureAtTile(map.Tile):F1}C < {MinColdestMonthTemp}C");
                return false; // too cold: coldest month dips below the habitable threshold
            }
            if (tile.rainfall < MinRainfall)
            {
                Log.Message($"[PMM_Slime] wander-in blocked: rainfall {tile.rainfall:F0}mm < {MinRainfall}mm");
                return false; // too dry: slimes need humid grasslands
            }
            return true;
        }

        // base.CanFireNowSub minus the SeasonAcceptableFor(Human) seasonal-temperature gate.
        // The vanilla wild-man event requires a non-colony humanlike faction to be the wild
        // man's "former faction"; slimes are factionless creatures, not ex-faction wild people,
        // so that requirement makes no sense here and only blocks the event on worlds without a
        // suitable faction. We keep the sensible environmental gates (toxic fallout, noxious
        // haze) and log which one fires.
        private bool BaseCanFireNowSub(IncidentParms parms)
        {
            if (!(parms.target is Map map))
            {
                Log.Message("[PMM_Slime] wander-in blocked: target is not a map");
                return false;
            }
            if (map.GameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout))
            {
                Log.Message("[PMM_Slime] wander-in blocked: toxic fallout active");
                return false;
            }
            if (ModsConfig.BiotechActive && map.GameConditionManager.ConditionIsActive(GameConditionDefOf.NoxiousHaze))
            {
                Log.Message("[PMM_Slime] wander-in blocked: noxious haze active");
                return false;
            }
            return true;
        }

        /// <summary>Same flow as the vanilla wild-man event, with a slime kind instead.</summary>
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!TryFindEntryCell(map, out IntVec3 cell))
            {
                return false;
            }

            // Former faction is optional flavour for the pawn's bio (relations, title origin);
            // slimes don't need one, so a world without a suitable faction still spawns them.
            TryFindFormerFaction(out Faction formerFaction);

            PawnKindDef kind = PawnKindToSpawn;
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind, formerFaction, PawnGenerationContext.NonPlayer, map.Tile,
                forceGenerateNewPawn: false, allowDead: false, allowDowned: false,
                canGeneratePawnRelations: true, mustBeCapableOfViolence: false,
                colonistRelationChanceFactor: 1f, forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true, allowPregnant: true, allowFood: true, allowAddictions: true,
                inhabitant: false, certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false,
                biocodeWeaponChance: 0f, biocodeApparelChance: 0f,
                extraPawnForExtraRelationChance: null, relationWithExtraPawnChanceFactor: 1f,
                validatorPreGear: null, validatorPostGear: null, forcedTraits: null,
                prohibitedTraits: null, minChanceToRedressWorldPawn: null,
                fixedBiologicalAge: null, fixedChronologicalAge: null, fixedGender: null,
                fixedLastName: null, fixedBirthName: null, fixedTitle: null,
                fixedIdeo: null, forceNoIdeo: false, forceNoBackstory: false,
                forbidAnyTitle: false, forceDead: false, forcedXenogenes: null,
                forcedEndogenes: null, forcedXenotype: null, forcedCustomXenotype: null,
                allowedXenotypes: null, forceBaselinerChance: 0f,
                // Vanilla passes DevelopmentalStage.Adult (value 8) here; it acts as a floor,
                // so generated wild pawns are always adults. (Newborn would force babies.)
                developmentalStages: DevelopmentalStage.Adult,
                forceNoGear: false));

            // Slimes are factionless. GeneratePawn with a null faction request can already
            // leave the pawn factionless, and Pawn.SetFaction logs a warning (popping the
            // debug log) when the new faction equals the current one - so only clear it
            // if generation actually assigned one (e.g. the former faction).
            if (pawn.Faction != null)
            {
                pawn.SetFaction(null, null);
            }
            GenSpawn.Spawn(pawn, cell, map);

            // Mark the wild slime as already having "reached outside" so it never tries
            // to march to the map edge and despawn (see Patch_SlimeShouldNotReachOutside).
            // Vanilla wild men get this cleared once arrested/tamed; here it simply keeps
            // the slime wandering the map until the player deals with it.
            if (pawn.mindState != null)
            {
                pawn.mindState.WildManEverReachedOutside = true;
            }

            // Use the incident def's own letter text ({0}=kind label, {PAWN_*} grammar tokens),
            // formatted the same way vanilla formats its wild-man letter.
            string kindLabel = pawn.KindLabel;
            TaggedString letterText = def.letterText.Formatted(kindLabel.Named("1"), pawn.Named("PAWN"))
                .AdjustedFor(pawn)
                .CapitalizeFirst();
            TaggedString letterLabel = def.letterLabel.Formatted(kindLabel.Named("0")).CapitalizeFirst();
            PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref letterText, ref letterLabel, pawn);
            SendStandardLetter(letterLabel, letterText, def.letterDef, parms, pawn);
            return true;
        }

        // The vanilla helpers are private; these are straight reimplementations.

        // Identical to the vanilla wild-man helper (which is private): any edge cell that
        // can reach the colony, ignoring roads. (My earlier version required a full walkable
        // path to the map edge and passed roadChance 0, which was too strict and made the
        // incident read as unavailable on many maps.)
        private static bool TryFindEntryCell(Map map, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomEdgeCellWith(
                c => map.reachability.CanReachColony(c),
                map, CellFinder.EdgeRoadChance_Ignore, out cell);
        }

        private static bool TryFindFormerFaction(out Faction formerFaction)
        {
            return Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(
                out formerFaction, tryMedievalOrBetter: false, allowDefeated: true, TechLevel.Undefined);
        }
    }

    /// <summary>
    /// A bubble slime wanders in. Same wild-man spawn flow as the common slime, but it
    /// only fires on maps that have BOTH caves and toxicity - the dark, filthy places
    /// bubble slimes call home - and always spawns the bubble-only pawn kind.
    /// Caves = enough overhead-mountain roof on the map; toxicity = world-tile pollution.
    /// </summary>
    public class IncidentWorker_BubbleSlimeWandersIn : IncidentWorker_SlimeWandersIn
    {
        /// <summary>
        /// Minimum world-tile pollution (0..1) to attract a bubble slime. 0.05 catches
        /// any visibly polluted tile (a handful of dissolved wastepacks is enough).
        /// </summary>
        private const float MinTilePollution = 0.05f;

        protected override PawnKindDef PawnKindToSpawn => DefDatabase<PawnKindDef>.GetNamed("PMM_SlimeWildBubble");

        /// <summary>Bubble slime climate gate: a polluted tile with caves on the map.</summary>
        protected override bool ClimateAcceptable(Map map)
        {
            float pollution = Find.WorldGrid[map.Tile].pollution;
            if (pollution < MinTilePollution)
            {
                Log.Message($"[PMM_Slime] bubble wander-in blocked: tile pollution {pollution:F2} < {MinTilePollution:F2}");
                return false; // too clean: bubble slimes want filth
            }
            if (!HasCaves(map))
            {
                Log.Message("[PMM_Slime] bubble wander-in blocked: no caves (overhead mountain) on the map");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// A taisui wanders in. Same wild-man spawn flow as the common slime, but it only
    /// fires on maps that have caves (overhead mountain) - the deep underground places
    /// Taisui endlessly circle - and always spawns the taisui-only pawn kind. No biome
    /// or climate gate: Taisui are not tied to surface weather, only to caves.
    /// </summary>
    public class IncidentWorker_TaisuiWandersIn : IncidentWorker_SlimeWandersIn
    {
        protected override PawnKindDef PawnKindToSpawn => DefDatabase<PawnKindDef>.GetNamed("PMM_SlimeWildTaisui");

        /// <summary>Taisui climate gate: any map that has caves.</summary>
        protected override bool ClimateAcceptable(Map map)
        {
            if (!HasCaves(map))
            {
                Log.Message("[PMM_Slime] taisui wander-in blocked: no caves (overhead mountain) on the map");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// A sea slime wanders in. Same wild-man spawn flow as the common slime, but it
    /// only fires on coastal tiles that border an ocean - the surf and shallows sea
    /// slimes call home - and always spawns the sea-only pawn kind. No biome or
    /// climate gate: sea slimes are tied to the sea, not to surface weather.
    /// </summary>
    public class IncidentWorker_SeaSlimeWandersIn : IncidentWorker_SlimeWandersIn
    {
        protected override PawnKindDef PawnKindToSpawn => DefDatabase<PawnKindDef>.GetNamed("PMM_SlimeWildSea");

        /// <summary>Sea slime climate gate: the world tile must border an ocean.</summary>
        protected override bool ClimateAcceptable(Map map)
        {
            if (!Find.WorldGrid[map.Tile].IsCoastal)
            {
                Log.Message("[PMM_Slime] sea slime wander-in blocked: tile is not coastal (does not border an ocean)");
                return false;
            }
            return true;
        }
    }
}
