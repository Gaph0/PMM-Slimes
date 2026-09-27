using System.Collections.Generic;
using ProjectMamono;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using VEF.Abilities;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// The dark slime's custom psycasts (Path of the Slime Core). Every ability
    /// leans on Project Mamono's public systems - TeaseApplication for tease
    /// damage, EssenceTransfer for essence/mana, Hediff_MamonoCorruption for the
    /// hidden corruption dose - so the numbers here feed the same economies as
    /// the mods' melee and infusion paths.
    ///
    /// SOFT DEPENDENCY: this file references VEF/VPE types and is only ever
    /// instantiated while Vanilla Psycasts Expanded is loaded (SlimeVPECompat).
    /// </summary>
    /// <summary>
    /// DefOfs for the SlimeCore psycast defs. Note the ascension ability def is
    /// NOT here: an AbilityDef field would have to be named PMM_PleasureAscension,
    /// which the hediff field already claims (DefOfs bind field name to defName).
    /// Nothing needs the ability def in code, so it stays out.
    /// </summary>
    [DefOf]
    public static class SlimePsycastDefOf
    {
        public static ThingDef PMM_SlimeWeb;
        public static HediffDef PMM_SlimeSlow;
        public static HediffDef PMM_AmoebaHold;
        public static HediffDef PMM_PleasureAscension;

        static SlimePsycastDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SlimePsycastDefOf));
        }
    }

    /// <summary>
    /// Shared helpers for the SlimeCore abilities.
    /// </summary>
    public static class SlimePsycastUtility
    {
        /// <summary>
        /// True when the target is under one of the slime effects that primes
        /// Pleasure Ascension: the Sticky Slime Bomb slow, the Amoeba Hold, or
        /// standing on a Draining Slime Splash web.
        /// </summary>
        public static bool IsSlimed(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            if (SlimePsycastDefOf.PMM_SlimeSlow != null &&
                pawn.health.hediffSet.HasHediff(SlimePsycastDefOf.PMM_SlimeSlow))
            {
                return true;
            }

            if (SlimePsycastDefOf.PMM_AmoebaHold != null &&
                pawn.health.hediffSet.HasHediff(SlimePsycastDefOf.PMM_AmoebaHold))
            {
                return true;
            }

            return OnSlimeWeb(pawn);
        }

        /// <summary>True when the pawn is standing on a slime web.</summary>
        public static bool OnSlimeWeb(Pawn pawn)
        {
            if (pawn?.Map == null || !pawn.Spawned || SlimePsycastDefOf.PMM_SlimeWeb == null)
            {
                return false;
            }

            List<Thing> things = pawn.Position.GetThingList(pawn.Map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].def == SlimePsycastDefOf.PMM_SlimeWeb)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Drains essence from the victim straight into the caster's Mana (the
        /// succubus feed: a dark slime's power tops her mana up directly). Moves
        /// only what the victim actually has, and clamps the mana bar.
        /// </summary>
        public static void DrainEssenceToMana(Pawn caster, Pawn victim, float amount)
        {
            Need_Essence essence = EssenceTransfer.Essence(victim);
            Need_Mana mana = EssenceTransfer.Mana(caster);
            if (essence == null || mana == null)
            {
                return;
            }

            float drained = Mathf.Min(essence.CurLevel, amount);
            if (drained <= 0f)
            {
                return;
            }

            essence.CurLevel -= drained;
            mana.CurLevel = Mathf.Clamp01(mana.CurLevel + drained);
        }

        /// <summary>
        /// The hidden corruption dose: advances the victim's corruption hediff by
        /// <paramref name="severity"/> (female targets only), imprints the
        /// caster's xenotype so finishing the job turns her into another slime,
        /// and remembers the caster as the source for join offers. Quiet by
        /// design - no letters, no mana cost (the psyfocus is the cost).
        /// </summary>
        public static void ApplyHiddenCorruption(Pawn caster, Pawn victim, float severity)
        {
            var settings = ProjectMamonoModSettings.Settings;
            if (settings == null || !settings.CorruptionEnabled)
            {
                return;
            }
            if (!MamonoTransformation.CanEverTransform(victim))
            {
                return;
            }

            Hediff_MamonoCorruption corruption = victim.health?.hediffSet?
                .GetFirstHediffOfDef(ProjectMamono_DefOf.ProjectMamono_MamonoCorruption) as Hediff_MamonoCorruption;
            if (corruption == null)
            {
                corruption = HediffMaker.MakeHediff(ProjectMamono_DefOf.ProjectMamono_MamonoCorruption, victim) as Hediff_MamonoCorruption;
                if (corruption == null)
                {
                    return;
                }
                victim.health.AddHediff(corruption);
            }

            corruption.Imprint(caster);
            corruption.Severity = Mathf.Min(corruption.Severity + severity, corruption.def.maxSeverity);

            if (corruption.Severity >= corruption.def.maxSeverity - 0.0001f)
            {
                corruption.CompleteTransformation();
            }
        }

        /// <summary>Applies the shared payload of every SlimeCore attack ability.</summary>
        public static void ApplyHit(Pawn caster, Pawn victim, float tease, float essenceDrain, float corruption)
        {
            if (victim == null)
            {
                return;
            }

            TeaseApplication.TryApplyTease(victim, caster, tease);
            DrainEssenceToMana(caster, victim, essenceDrain);
            ApplyHiddenCorruption(caster, victim, corruption);
        }
    }

    /// <summary>
    /// Sticky Slime Bomb: AoE lob that slows everything caught and splashes
    /// tease damage (+ a whisper of corruption on women).
    /// </summary>
    public class Ability_StickySlimeBomb : Ability
    {
        private const float TeaseDamage = 0.10f;
        private const float HiddenCorruption = 0.05f;
        private const int SlowTicks = 600; // 10s

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);

            Map map = CasterPawn?.Map;
            if (map == null || targets == null || targets.Length == 0)
            {
                return;
            }

            IntVec3 center = targets[0].Cell;
            float radius = GetRadiusForPawn();

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn pawn && pawn != CasterPawn && pawn.RaceProps.Humanlike)
                    {
                        SlimePsycastUtility.ApplyHit(CasterPawn, pawn, TeaseDamage, 0f, HiddenCorruption);
                        ApplySlow(pawn);
                    }
                }
            }
        }

        private void ApplySlow(Pawn pawn)
        {
            var slow = pawn.health.hediffSet.GetFirstHediffOfDef(SlimePsycastDefOf.PMM_SlimeSlow) as Hediff_SlimeSlow;
            if (slow == null)
            {
                slow = HediffMaker.MakeHediff(SlimePsycastDefOf.PMM_SlimeSlow, pawn) as Hediff_SlimeSlow;
                pawn.health.AddHediff(slow);
            }
            slow?.Refresh(SlowTicks);
        }
    }

    /// <summary>
    /// Slime Grope: a single-target yank of tease damage and essence.
    /// </summary>
    public class Ability_SlimeGrope : Ability
    {
        private const float TeaseDamage = 0.20f;
        private const float EssenceDrain = 0.10f;
        private const float HiddenCorruption = 0.10f;

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            SlimePsycastUtility.ApplyHit(CasterPawn, FirstTargetPawn(targets), TeaseDamage, EssenceDrain, HiddenCorruption);
        }

        /// <summary>The pawn in the first cast target, or null.</summary>
        protected static Pawn FirstTargetPawn(GlobalTargetInfo[] targets)
        {
            return targets != null && targets.Length > 0 ? targets[0].Thing as Pawn : null;
        }
    }

    /// <summary>
    /// Amoeba Hold: engulfs an adjacent pawn, rooting both caster and victim
    /// while the hold lasts. Ends early if either side goes down or dies.
    /// </summary>
    public class Ability_AmoebaHold : Ability
    {
        private const float TeaseDamage = 0.10f;
        private const float EssenceDrain = 0.20f;
        private const float HiddenCorruption = 0.10f;
        private const int HoldTicks = 300; // 5s

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);

            Pawn caster = CasterPawn;
            Pawn victim = targets != null && targets.Length > 0 ? targets[0].Thing as Pawn : null;
            if (caster == null || victim == null)
            {
                return;
            }

            SlimePsycastUtility.ApplyHit(caster, victim, TeaseDamage, EssenceDrain, HiddenCorruption);
            ApplyHold(caster, victim);
            ApplyHold(victim, caster);
        }

        private void ApplyHold(Pawn rooted, Pawn partner)
        {
            var hold = rooted.health.hediffSet.GetFirstHediffOfDef(SlimePsycastDefOf.PMM_AmoebaHold) as Hediff_AmoebaHold;
            if (hold == null)
            {
                hold = HediffMaker.MakeHediff(SlimePsycastDefOf.PMM_AmoebaHold, rooted) as Hediff_AmoebaHold;
                rooted.health.AddHediff(hold);
            }
            hold?.Refresh(HoldTicks, partner);
        }
    }

    /// <summary>
    /// Pleasure Ascension: the capstone. Only castable on a pawn already coated
    /// in slime (bomb slow, hold, or standing in a splash web); binds them and
    /// wrings them out with overwhelming pleasure, corrupting women three
    /// quarters of the way to becoming a slime.
    /// </summary>
    public class Ability_PleasureAscension : Ability
    {
        private const int BindTicks = 1200; // 20s

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages))
            {
                return false;
            }

            Pawn pawn = target.Pawn;
            if (pawn == null)
            {
                return false;
            }

            if (!SlimePsycastUtility.IsSlimed(pawn))
            {
                if (showMessages)
                {
                    Messages.Message("PMM_PleasureAscensionNeedsSlime".Translate(pawn.LabelShort), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return true;
        }

        public override bool AICanUseOn(Thing target)
        {
            return base.AICanUseOn(target) && target is Pawn pawn && SlimePsycastUtility.IsSlimed(pawn);
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);

            Pawn caster = CasterPawn;
            Pawn victim = targets != null && targets.Length > 0 ? targets[0].Thing as Pawn : null;
            if (caster == null || victim == null)
            {
                return;
            }

            var ascension = HediffMaker.MakeHediff(SlimePsycastDefOf.PMM_PleasureAscension, victim) as Hediff_PleasureAscension;
            if (ascension == null)
            {
                return;
            }
            victim.health.AddHediff(ascension);
            ascension.Refresh(BindTicks, caster);
        }
    }

    /// <summary>
    /// Draining Slime Splash: lays down a patch of living slime that keeps
    /// lapping at anyone standing in it, teasing them and sucking their essence
    /// back to the caster.
    /// </summary>
    public class Ability_DrainingSlimeSplash : Ability
    {
        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);

            Map map = CasterPawn?.Map;
            if (map == null || targets == null || targets.Length == 0)
            {
                return;
            }

            IntVec3 center = targets[0].Cell;
            float radius = GetRadiusForPawn();
            int duration = GetDurationForPawn();

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.Filled(map))
                {
                    continue;
                }
                if (cell.GetFirstThing(map, SlimePsycastDefOf.PMM_SlimeWeb) != null)
                {
                    continue;
                }

                var web = (SlimeWeb)ThingMaker.MakeThing(SlimePsycastDefOf.PMM_SlimeWeb);
                web.Setup(CasterPawn, duration);
                GenSpawn.Spawn(web, cell, map);
            }
        }
    }

    /// <summary>
    /// The tease-coated snare left by Sticky Slime Bomb. Roots the victim by
    /// slowing them to a crawl while the gel clings on.
    /// </summary>
    public class Hediff_SlimeSlow : HediffWithComps
    {
        private int ticksRemaining;

        public override bool ShouldRemove => ticksRemaining <= 0;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            ticksRemaining -= delta;
        }

        public void Refresh(int ticks)
        {
            ticksRemaining = Mathf.Max(ticksRemaining, ticks);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
        }
    }

    /// <summary>
    /// The mutual root of Amoeba Hold. Both participants carry one; when either
    /// side goes down, dies or leaves the map, the partner's hold dissolves too.
    /// </summary>
    public class Hediff_AmoebaHold : HediffWithComps
    {
        private int ticksRemaining;
        private Pawn partner;

        public override bool ShouldRemove => ticksRemaining <= 0;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            ticksRemaining -= delta;

            if (partner != null && pawn != null && pawn.IsHashIntervalTick(30, delta))
            {
                if (partner.Dead || partner.Downed || !partner.Spawned || partner.Map != pawn.Map)
                {
                    ticksRemaining = 0;
                }
            }
        }

        public void Refresh(int ticks, Pawn newPartner)
        {
            ticksRemaining = Mathf.Max(ticksRemaining, ticks);
            partner = newPartner;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
            Scribe_References.Look(ref partner, "partner");
        }
    }

    /// <summary>
    /// The 20-second bind of Pleasure Ascension: waves of unbelievable pleasure
    /// (0.4 tease every 10 seconds) that end in a guaranteed willpower knockout,
    /// and - on women - a corruption dose that leaves her one infusion short of
    /// becoming a slime herself.
    /// </summary>
    public class Hediff_PleasureAscension : HediffWithComps
    {
        private const int WaveInterval = 600;   // 10s
        private const float TeasePerWave = 0.40f;
        private const float CorruptionTotal = 0.75f;

        private int ticksRemaining;
        private Pawn caster;

        public override bool ShouldRemove => ticksRemaining <= 0;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            ticksRemaining -= delta;

            if (pawn == null || pawn.Dead)
            {
                ticksRemaining = 0;
                return;
            }

            if (ticksRemaining % WaveInterval < delta)
            {
                TeaseApplication.TryApplyTease(pawn, caster, TeasePerWave);
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (caster != null && pawn != null && !pawn.Dead)
            {
                SlimePsycastUtility.ApplyHiddenCorruption(caster, pawn, CorruptionTotal);
            }
        }

        public void Refresh(int ticks, Pawn newCaster)
        {
            ticksRemaining = Mathf.Max(ticksRemaining, ticks);
            caster = newCaster;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
            Scribe_References.Look(ref caster, "caster");
        }
    }

    /// <summary>
    /// One tile of the living slime laid down by Draining Slime Splash. Laps at
    /// whoever stands on it: a slow trickle of tease, essence siphoned back to
    /// the caster, and a faint corrupting seep on women. Dries up when its
    /// duration ends.
    /// </summary>
    public class SlimeWeb : ThingWithComps
    {
        private const int LashInterval = 60;    // 1s
        private const float TeasePerLash = 0.005f; // 0.5 tease over a full 15s stand
        private const float EssencePerLash = 0.01f;
        private const float CorruptionPerLash = 0.005f; // 0.05 over a full stand

        private Pawn caster;
        private int ticksRemaining;

        public void Setup(Pawn newCaster, int durationTicks)
        {
            caster = newCaster;
            ticksRemaining = durationTicks;
        }

        protected override void Tick()
        {
            base.Tick();
            ticksRemaining--;
            if (ticksRemaining <= 0)
            {
                Destroy();
                return;
            }

            if (!this.IsHashIntervalTick(LashInterval))
            {
                return;
            }

            List<Thing> things = Position.GetThingList(Map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Pawn pawn && pawn != caster && pawn.RaceProps.Humanlike && !pawn.Dead)
                {
                    SlimePsycastUtility.ApplyHit(caster, pawn, TeasePerLash, EssencePerLash, CorruptionPerLash);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
        }
    }
}
