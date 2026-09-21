using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// "She is more likely to be encountered when she becomes tangled in a fisherman's
    /// nets." A postfix on the private JobDriver_Fish.CompleteFishingToil — the exact
    /// point where a fishing catch is generated — that gives each completed catch a
    /// small chance to haul up a sea slime instead, on maps bordering an ocean.
    /// (CompleteFishingToil is private, so the patch target is resolved by name through
    /// AccessTools.Method rather than a compile-time nameof.)
    ///
    /// 6% per catch (the same feel as the vanilla rare-table corpse catch, which uses
    /// FishingUtility.ChanceForRareCatch = 1%): common enough to actually happen, rare
    /// enough to stay a surprise. The slime spawns at the fished water cell, factionless
    /// and already "reached outside" so she wanders the map instead of walking off it.
    ///
    /// Soft Odyssey compat: fishing is Odyssey content, and the patch is a no-op when
    /// Odyssey is inactive (JobDriver_Fish never runs). Everything is wrapped in a
    /// try/catch so a failure can never break the fishing toil itself.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_SeaSlimeFishing
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(JobDriver_Fish), "CompleteFishingToil");
        }

        /// <summary>Chance a completed fishing catch hauls up a sea slime (0.06 = 6%).</summary>
        private const float SeaSlimeCatchChance = 0.06f;

        public static void Postfix(JobDriver_Fish __instance)
        {
            TryHaulUpSeaSlime(__instance);
        }

        private static void TryHaulUpSeaSlime(JobDriver_Fish __instance)
        {
            try
            {
                if (!ModsConfig.OdysseyActive)
                {
                    return;
                }

                Pawn fisher = __instance?.pawn;
                Map map = fisher?.Map;
                if (fisher == null || map == null || !fisher.Spawned || fisher.Dead)
                {
                    return;
                }
                // Only colonists / player-faction fishers tangle a sea slime.
                if (fisher.Faction != Faction.OfPlayer)
                {
                    return;
                }
                // Sea slimes only turn up where there is open sea: an ocean-bordering tile.
                if (!Find.WorldGrid[map.Tile].IsCoastal)
                {
                    return;
                }
                if (!Rand.Chance(SeaSlimeCatchChance))
                {
                    return;
                }

                SpawnSeaSlime(fisher, map, __instance.job);
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce($"[PMM_Slime_Blue] sea slime fishing catch failed: {e}", 94108372);
            }
        }

        /// <summary>Generate the sea slime and drop her at the fished water cell.</summary>
        private static void SpawnSeaSlime(Pawn fisher, Map map, Job job)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("PMM_Slime_Sea");
            if (kind == null)
            {
                return;
            }

            // The cell the fisher was targeting (the water, target A), falling back to the fisher.
            IntVec3 cell = job != null ? job.GetTarget(TargetIndex.A).Cell : fisher.Position;
            if (!cell.IsValid || !cell.InBounds(map) || !cell.Walkable(map))
            {
                cell = fisher.Position;
            }

            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind, null, PawnGenerationContext.NonPlayer, map.Tile,
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
                developmentalStages: DevelopmentalStage.Adult, forceNoGear: false));

            if (pawn.Faction != null)
            {
                pawn.SetFaction(null, null);
            }
            GenSpawn.Spawn(pawn, cell, map);

            // Mark the wild slime as already having "reached outside" so she wanders the
            // map instead of marching to the edge and despawning (same as the wander-ins).
            if (pawn.mindState != null)
            {
                pawn.mindState.WildManEverReachedOutside = true;
            }

            Find.LetterStack.ReceiveLetter(
                "LetterLabelSeaSlimeCatch".Translate(),
                "LetterSeaSlimeCatch".Translate(fisher.Named("FISHER"), pawn.Named("SLIME")),
                LetterDefOf.NeutralEvent, pawn);
        }
    }
}
