using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// Shared bubble-slime helpers: who counts as a bubble slime, and the bond check
    /// that makes a pawn tolerant of bubble slime jelly.
    /// </summary>
    public static class BubbleSlimeUtility
    {
        /// <summary>
        /// True for bubble slimes. Checks the race def first (set by the generation
        /// patch), with a xenotype fallback for pawns spawned before the patch existed.
        /// </summary>
        public static bool IsBubbleSlime(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }
            return pawn.def == SlimeDefOf.PMM_Race_SlimeMomoBubble ||
                   pawn.genes?.Xenotype?.defName == "PMM_SlimeBubble";
        }

        /// <summary>
        /// True if the pawn is bonded to a bubble slime. "Bonded" covers both Project
        /// Momo's Tsugai bond (the mod's husband/wife-style bond, labelled "bonded" on
        /// the Social tab) and the vanilla animal Bond relation, in case a slime ever
        /// ends up on the animal-bond path. Such a pawn's body has grown used to the
        /// slime's toxins and can eat bubble slime jelly without getting sick.
        /// </summary>
        public static bool IsBondedToBubbleSlime(Pawn pawn)
        {
            if (pawn?.relations == null)
            {
                return false;
            }
            List<DirectPawnRelation> relations = pawn.relations.DirectRelations;
            for (int i = 0; i < relations.Count; i++)
            {
                DirectPawnRelation rel = relations[i];
                if ((rel.def == ProjectMomo.ProjectMomo_DefOf.ProjectMomo_Tsugai ||
                     rel.def == PawnRelationDefOf.Bond) && IsBubbleSlime(rel.otherPawn))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Bubble slime jelly is tainted: guaranteed food poisoning on ingestion. The only
    /// exception is a pawn bonded to a bubble slime (Tsugai or animal bond) - their
    /// body has adapted to the slime's toxins, so the base GiveHediff effect is skipped.
    /// Wired up in ThingDefs_SlimeJelly.xml on PMM_SlimeJellyBubble.
    /// </summary>
    public class IngestionOutcomeDoer_BubbleJellyFoodPoisoning : IngestionOutcomeDoer_GiveHediff
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (BubbleSlimeUtility.IsBondedToBubbleSlime(pawn))
            {
                return; // adapted to the slime's toxins: no food poisoning
            }
            base.DoIngestionOutcomeSpecial(pawn, ingested, ingestedCount);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(ThingDef ingested)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(ingested))
            {
                yield return entry;
            }
            yield return new StatDrawEntry(StatCategoryDefOf.Basics,
                "Causes food poisoning", "unless bonded to a bubble slime",
                "Bubble slime jelly is laced with the slime's toxins: anyone who eats it gets food poisoning. " +
                "Pawns bonded to a bubble slime have adapted to the toxins and can eat it safely.",
                0);
        }
    }

    /// <summary>
    /// Bubble slime jelly flushes the eater's system: it reduces any accumulated
    /// toxic buildup (the ToxicBuildup hediff) to zero, mirroring the slime's own
    /// toxin-processing body. Independent of the food-poisoning / bond logic - even
    /// someone who gets sick from the jelly still has their buildup purged.
    /// Wired up in ThingDefs_SlimeJelly.xml on PMM_SlimeJellyBubble.
    /// </summary>
    public class IngestionOutcomeDoer_RemoveToxBuildup : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            Hediff buildup = pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);
            if (buildup != null)
            {
                buildup.Severity = 0f;
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(ThingDef ingested)
        {
            yield return new StatDrawEntry(StatCategoryDefOf.Basics,
                "Purges toxic buildup", "reduces to zero",
                "The slime's antitoxins flush all accumulated toxic buildup from the eater's body.",
                0);
        }
    }

    public class CompProperties_ConsumeWastepack : CompProperties
    {
        /// <summary>How far (in cells) the slime can reach a wastepack to devour.</summary>
        public float searchRadius = 20f;

        /// <summary>Toxic gas emitted per consumed wastepack (10% of the old amount).</summary>
        public float gasPerPack = 2f;

        /// <summary>Cooldown between uses, in in-game hours.</summary>
        public float cooldownHours = 12f;

        public CompProperties_ConsumeWastepack()
        {
            compClass = typeof(CompConsumeWastepack);
        }
    }

    /// <summary>
    /// Gives player-owned bubble slimes a "consume wastepack" gizmo: target a stack of
    /// toxic wastepacks and the slime devours the whole stack, dissolving it into its
    /// body. The reaction bubbles off a small cloud of toxic gas (10% of the old burst)
    /// and the slime then needs 12 in-game hours to process before it can feed again.
    /// On the bubble race def (Race_SlimeMomo.xml), so every bubble slime has it; the
    /// gizmo only shows for player-faction pawns.
    /// </summary>
    public class CompConsumeWastepack : ThingComp
    {
        private int lastConsumeTick = -999999;

        public CompProperties_ConsumeWastepack Props => (CompProperties_ConsumeWastepack)props;

        private int CooldownTicks => (int)(Props.cooldownHours * GenDate.TicksPerHour);

        private bool OnCooldown => Find.TickManager.TicksGame < lastConsumeTick + CooldownTicks;

        private int CooldownTicksRemaining => (lastConsumeTick + CooldownTicks) - Find.TickManager.TicksGame;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // Wastepacks and tox gas are Biotech content; also player-faction only.
            if (!ModsConfig.BiotechActive || !(parent is Pawn pawn) ||
                pawn.Faction != Faction.OfPlayer || pawn.Dead || !pawn.Spawned)
            {
                yield break;
            }

            var command = new Command_Action
            {
                defaultLabel = "Consume wastepack",
                defaultDesc = "Order the bubble slime to devour a stack of toxic wastepacks, dissolving " +
                              "it into its body. The reaction releases a small cloud of toxic gas. " +
                              $"Usable once every {Props.cooldownHours:F0} hours.",
                icon = ThingDefOf.Wastepack.uiIcon,
                action = delegate { StartTargeting(pawn); }
            };
            if (OnCooldown)
            {
                command.Disable($"Recharging ({CooldownTicksRemaining.ToStringTicksToPeriod()}).");
            }
            else if (!AnyWastepackInReach(pawn))
            {
                command.Disable($"No toxic wastepack within {Props.searchRadius:F0} cells.");
            }
            yield return command;
        }

        /// <summary>True if any spawned wastepack is within reach (for the disable reason).</summary>
        private bool AnyWastepackInReach(Pawn pawn)
        {
            List<Thing> packs = pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Wastepack);
            for (int i = 0; i < packs.Count; i++)
            {
                if (!packs[i].Destroyed &&
                    pawn.Position.DistanceTo(packs[i].Position) <= Props.searchRadius)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Begin targeting: pick a wastepack stack within reach and consume it.</summary>
        private void StartTargeting(Pawn pawn)
        {
            var targetingParameters = new TargetingParameters
            {
                canTargetItems = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetLocations = false,
                mapObjectTargetsMustBeAutoAttackable = false,
                validator = target =>
                    target.HasThing && target.Thing.def == ThingDefOf.Wastepack &&
                    pawn.Position.DistanceTo(target.Thing.Position) <= Props.searchRadius
            };

            // BeginTargeting(targetParams, action, caster, actionWhenFinished, mouseAttachment, requiresCastedSelected).
            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                if (target.HasThing)
                {
                    Consume(pawn, target.Thing);
                }
            }, caster: pawn, actionWhenFinished: null, mouseAttachment: null, requiresCastedSelected: true);
        }

        /// <summary>Devour the whole stack and burp out a small cloud of toxic gas.</summary>
        private void Consume(Pawn pawn, Thing pack)
        {
            if (pack == null || pack.Destroyed)
            {
                return;
            }
            IntVec3 position = pawn.Position;
            Map map = pawn.Map;
            int packsConsumed = pack.stackCount;

            pack.Destroy();
            lastConsumeTick = Find.TickManager.TicksGame;

            int gasAmount = (int)(packsConsumed * Props.gasPerPack);
            if (gasAmount > 0)
            {
                GasUtility.AddGas(position, map, GasType.ToxGas, gasAmount);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastConsumeTick, "lastConsumeTick", -999999);
        }
    }
}
