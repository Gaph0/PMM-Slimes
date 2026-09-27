using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// The jelly-oozing hediff: a passive marker that drives periodic jelly production
    /// via its comp. Never shown on the Health tab (it's a production mechanic, not an
    /// injury or condition), so <see cref="Hediff.Visible"/> is overridden to false.
    /// </summary>
    public class Hediff_SlimeJellyOozing : HediffWithComps
    {
        public override bool Visible => false;
    }

    /// <summary>
    /// Periodic slime jelly production. Driven by the hidden PMM_Hediff_SlimeJellyOozing
    /// hediff that the slime gel gene adds (and removes), so any gene carrier oozes.
    /// The health tracker stops ticking at death, so corpses never produce jelly.
    /// </summary>
    public class HediffCompProperties_SlimeJellyProduction : HediffCompProperties
    {
        /// <summary>Days between jelly drops.</summary>
        public float intervalDays = 1f;

        /// <summary>How much jelly is dropped each interval.</summary>
        public IntRange jellyCount = new IntRange(2, 4);

        public HediffCompProperties_SlimeJellyProduction()
        {
            compClass = typeof(HediffComp_SlimeJellyProduction);
        }
    }

    public class HediffComp_SlimeJellyProduction : HediffComp
    {
        private int ticksUntilNextDrop = -1;

        public HediffCompProperties_SlimeJellyProduction Props =>
            (HediffCompProperties_SlimeJellyProduction)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            if (ticksUntilNextDrop < 0)
            {
                // First tick after being added: roll a fresh countdown.
                ticksUntilNextDrop = IntervalTicks;
            }

            ticksUntilNextDrop -= delta;
            if (ticksUntilNextDrop > 0)
            {
                return;
            }

            ticksUntilNextDrop = IntervalTicks;
            DropJelly(pawn);
        }

        private int IntervalTicks => (int)(Props.intervalDays * GenDate.TicksPerDay);

        private void DropJelly(Pawn pawn)
        {
            // Drop the jelly matching this slime's xenotype (blue/bubble/red/dark/taisui);
            // anything without a recognised xenotype falls back to blue slime jelly.
            Thing jelly = ThingMaker.MakeThing(SlimeRaces.JellyFor(pawn?.genes?.Xenotype?.defName));
            jelly.stackCount = Props.jellyCount.RandomInRange;

            IntVec3 cell = pawn.Position;
            if (!cell.Walkable(pawn.Map) && !CellFinder.TryFindRandomReachableNearbyCell(
                    cell, pawn.Map, 2f, TraverseParms.For(pawn), c => c.Walkable(pawn.Map), null, out cell))
            {
                cell = pawn.Position; // nowhere better: drop it under the slime anyway
            }
            GenPlace.TryPlaceThing(jelly, cell, pawn.Map, ThingPlaceMode.Near);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksUntilNextDrop, "ticksUntilNextDrop", -1);
        }
    }

    /// <summary>
    /// Eating slime jelly monsterises women who consume too much. Each jelly adds
    /// severity to Project Mamono's mamono corruption hediff, imprinted with the slime
    /// xenotype, so a woman who binges melts into a slime. The corruption decays while
    /// she is upright, so casual snacking is safe - only sustained consumption completes
    /// the change. Men, children and monsters are unaffected (CanEverTransform gate).
    /// </summary>
    public class IngestionOutcomeDoer_SlimeJelly : IngestionOutcomeDoer
    {
        /// <summary>Corruption severity added per jelly eaten (XML-tunable).</summary>
        public float severityPerJelly = 0.08f;

        // Hediff_MamonoCorruption stores its imprint target in the private targetXenotypeDefName
        // field, declared on the subclass (ProjectMamono.Hediff_MamonoCorruption) - a FieldRef
        // resolved on the base Hediff type would fail (the field isn't there). Resolve the
        // FieldInfo on the subclass first, then bind a Hediff-typed FieldRef to it.
        private static readonly AccessTools.FieldRef<Hediff, string> CorruptionTargetRef =
            AccessTools.FieldRefAccess<Hediff, string>(
                AccessTools.Field(
                    AccessTools.TypeByName("ProjectMamono.Hediff_MamonoCorruption"),
                    "targetXenotypeDefName"));

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (pawn == null || pawn.Dead || !ProjectMamono.MamonoTransformation.CanEverTransform(pawn))
            {
                return;
            }

            HediffDef corruptionDef = ProjectMamono.ProjectMamono_DefOf.ProjectMamono_MamonoCorruption;
            Hediff corruption = pawn.health?.hediffSet?.GetFirstHediffOfDef(corruptionDef);
            if (corruption == null)
            {
                corruption = HediffMaker.MakeHediff(corruptionDef, pawn);
                corruption.Severity = 0f;
                pawn.health.AddHediff(corruption);
            }

            // Imprint the slime xenotype matching this jelly: she melts into that
            // slime's colour, not a base Mamono (blue jelly -> blue slime, taisui jelly
            // -> taisui, and so on).
            if (corruption is ProjectMamono.Hediff_MamonoCorruption)
            {
                CorruptionTargetRef(corruption) = SlimeRaces.XenotypeFor(ingested?.def);
            }

            corruption.Severity += severityPerJelly * ingestedCount;

            // Full severity: fire the transformation now rather than waiting for the
            // corruption hediff's own next 250-tick re-check.
            if (corruption.Severity >= corruption.def.maxSeverity - 0.0001f &&
                corruption is ProjectMamono.Hediff_MamonoCorruption mamonoCorruption)
            {
                mamonoCorruption.CompleteTransformation();
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(ThingDef ingested)
        {
            yield return new StatDrawEntry(StatCategoryDefOf.Basics,
                "Mamono corruption", (severityPerJelly * 100f).ToString("0") + " per jelly (women only)",
                "Women who eat too much slime jelly are slowly monsterised into slimes.", 0);
        }
    }
}
