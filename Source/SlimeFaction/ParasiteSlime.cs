using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// The slime carrier: a human woman infested and remade by a parasite slime. Unlike
    /// every other slime in the mod she is defined by what she KEEPS - her Human race,
    /// xenotype (usually Baseliner), backstories, name, hair, skin colour and genes are
    /// all untouched. She gains exactly two genes (Momo + slime gel, as plain endogenes).
    ///
    /// Lifecycle (three stages), shared by the visit event and the item:
    ///   1. SECRET:    she carries the hidden PMM_Hediff_ParasiteCarrier marker but has
    ///                 NO slime/Momo genes yet - so nothing shows on her Bio tab and her
    ///                 physiology is still human. The event masks her real backstories
    ///                 with "unknown" placeholders; the item path keeps her own.
    ///   2. COUNTDOWN: accepting her (event) or eating the item (colonist) starts the
    ///                 hidden PMM_Hediff_ParasiteTakeover - a ~2-day timer. She works as
    ///                 a normal colonist meanwhile (fertile, no slime traits).
    ///   3. TURN:      the takeover completes, <see cref="ApplyCarrierGenes"/> adds the
    ///                 two genes, the full carrier physiology switches on, and an alert
    ///                 letter fires. Rejecting her never reaches this stage: she leaves
    ///                 quietly, still an ordinary woman (decision R2).
    /// </summary>
    public static class SlimeCarrierUtility
    {
        /// <summary>True if the pawn carries the parasite (secret or turned).</summary>
        public static bool IsCarrier(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(SlimeDefOf.PMM_Hediff_ParasiteCarrier) != null;
        }

        /// <summary>True while the parasite's presence is still hidden from the player.</summary>
        public static bool IsSecretCarrier(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(SlimeDefOf.PMM_Hediff_ParasiteCarrier)
                is Hediff_ParasiteCarrier carrier && !carrier.revealed;
        }

        /// <summary>True once the genes have landed (the carrier physiology is active).</summary>
        public static bool IsTurnedCarrier(Pawn pawn)
        {
            return IsCarrier(pawn) && Gene_SlimeGel.IsSlime(pawn);
        }

        /// <summary>
        /// Who a parasite slime can infest: a living humanlike woman who is not already
        /// a slime (a real slime's body offers no purchase) and not already a carrier.
        /// Pregnancy is no obstacle: an existing pregnancy runs its course during the
        /// countdown, but the turned carrier can never conceive again.
        /// </summary>
        public static bool CanBeInfested(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.RaceProps.Humanlike)
            {
                return false;
            }
            if (pawn.gender != Gender.Female)
            {
                return false;
            }
            if (Gene_SlimeGel.IsSlime(pawn) || IsCarrier(pawn))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// STAGE 1 - seed the secret carrier: add only the hidden marker hediff. No genes,
        /// so her Bio tab stays clean and her physiology stays human. The takeover that
        /// turns her is started separately (<see cref="BeginTakeover"/>) by the relevant
        /// path (event acceptance, or item ingestion). Idempotent.
        /// </summary>
        public static void MakeCarrier(Pawn pawn, bool revealed)
        {
            if (pawn?.health == null || pawn.Dead)
            {
                return;
            }
            if (GetCarrierHediff(pawn) != null)
            {
                return; // already carries the parasite
            }
            Hediff_ParasiteCarrier carrier =
                (Hediff_ParasiteCarrier)HediffMaker.MakeHediff(SlimeDefOf.PMM_Hediff_ParasiteCarrier, pawn);
            carrier.revealed = revealed;
            pawn.health.AddHediff(carrier);
        }

        /// <summary>
        /// STAGE 2 - begin the hidden ~2-day takeover that ends in the genes landing.
        /// Safe to call more than once (no-op if a takeover or a turn is already running).
        /// </summary>
        public static void BeginTakeover(Pawn pawn)
        {
            if (pawn?.health == null || pawn.Dead)
            {
                return;
            }
            if (IsTurnedCarrier(pawn) ||
                pawn.health.hediffSet.GetFirstHediffOfDef(SlimeDefOf.PMM_Hediff_ParasiteTakeover) != null)
            {
                return; // already turning or turned
            }
            pawn.health.AddHediff(SlimeDefOf.PMM_Hediff_ParasiteTakeover);
        }

        /// <summary>
        /// STAGE 3 - the parasite finishes remaking her: add the Momo and slime-gel genes
        /// as plain endogenes (she keeps every gene she already had), flip the marker
        /// visible, and fire the turn alert. Called by the takeover hediff at full
        /// severity; also usable directly by dev tools. Order matters:
        ///  1. snapshot hair/body (Gene_Momo.PostAdd re-styles non-feminine hair);
        ///  2. add the genes - the carrier-marker guard in Gene_SlimeGel.PostAdd is
        ///     already true (the marker was added in Stage 1), so no jelly-oozing hediff;
        ///  3. restore her hair/body: the woman she was, on the outside.
        /// </summary>
        public static void ApplyCarrierGenes(Pawn pawn)
        {
            if (pawn?.health == null || pawn.Dead || pawn.genes == null)
            {
                return;
            }
            Hediff_ParasiteCarrier carrier = GetCarrierHediff(pawn);
            if (carrier == null)
            {
                // A takeover without the marker (dev edit): seed it so the guards work.
                MakeCarrier(pawn, revealed: true);
                carrier = GetCarrierHediff(pawn);
            }

            HairDef hair = pawn.story?.hairDef;
            BodyTypeDef body = pawn.story?.bodyType;

            if (!pawn.genes.HasActiveGene(ProjectMomo.ProjectMomo_DefOf.ProjectMomo_Momo))
            {
                pawn.genes.AddGene(ProjectMomo.ProjectMomo_DefOf.ProjectMomo_Momo, xenogene: false);
            }
            if (!pawn.genes.HasActiveGene(SlimeDefOf.PMM_Gene_SlimeGel))
            {
                pawn.genes.AddGene(SlimeDefOf.PMM_Gene_SlimeGel, xenogene: false);
            }

            if (pawn.story != null)
            {
                if (hair != null && pawn.story.hairDef != hair)
                {
                    pawn.story.hairDef = hair;
                }
                if (body != null && pawn.story.bodyType != body)
                {
                    pawn.story.bodyType = body;
                }
            }

            // The moment of truth: the marker surfaces and the colony is told.
            carrier.revealed = true;
            Find.LetterStack.ReceiveLetter(
                "PMM_LetterLabelCarrierTurn".Translate(),
                "PMM_LetterCarrierTurn".Translate(pawn.Named("PAWN")).AdjustedFor(pawn),
                pawn.Faction == Faction.OfPlayer ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent,
                pawn);
        }

        /// <summary>The marker hediff instance, or null.</summary>
        public static Hediff_ParasiteCarrier GetCarrierHediff(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(SlimeDefOf.PMM_Hediff_ParasiteCarrier)
                as Hediff_ParasiteCarrier;
        }
    }

    /// <summary>
    /// Eating a parasite slime. A valid host (a humanlike woman who is neither slime nor
    /// carrier) is seeded as a secret carrier and the silent ~2-day takeover begins; she
    /// notices nothing until it completes. Anyone else - men, children, existing slimes,
    /// existing carriers - just eats a foul-tasting blob; the item is consumed with no
    /// effect beyond a message.
    /// </summary>
    public class IngestionOutcomeDoer_ParasiteSlime : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (!SlimeCarrierUtility.CanBeInfested(pawn))
            {
                Messages.Message(
                    "PMM_ParasiteSlimeNoEffect".Translate(pawn.Named("PAWN")).AdjustedFor(pawn),
                    pawn, MessageTypeDefOf.NeutralEvent, historical: false);
                return;
            }
            // Seed the parasite (hidden marker) and start the countdown to the turn.
            SlimeCarrierUtility.MakeCarrier(pawn, revealed: false);
            SlimeCarrierUtility.BeginTakeover(pawn);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(ThingDef ingested)
        {
            yield return new StatDrawEntry(StatCategoryDefOf.Basics,
                "PMM_ParasiteSlimeStatLabel".Translate(),
                "PMM_ParasiteSlimeStatValue".Translate(),
                "PMM_ParasiteSlimeStatDesc".Translate(), 0);
        }
    }

    /// <summary>
    /// The hidden takeover countdown. Invisible and symptomless while severity climbs
    /// (~2 days, driven by the vanilla SeverityPerDay comp in the def); at full severity
    /// the parasite finishes remaking her (<see cref="SlimeCarrierUtility.ApplyCarrierGenes"/>)
    /// and this hediff removes itself.
    ///
    /// Overrides PostTickInterval, NOT TickInterval: in RimWorld 1.6 the pawn health
    /// tracker drives hediffs via PostTickInterval(delta), and HediffWithComps.PostTickInterval
    /// is what ticks the comps (CompPostTickInterval) and applies their severityAdjustment.
    /// Base Hediff.TickInterval does NOT call PostTickInterval, so overriding TickInterval
    /// would leave the SeverityPerDay comp unticked and severity would never climb - the
    /// exact "never transforms" bug. Must extend HediffWithComps for the comps to load.
    /// </summary>
    public class Hediff_ParasiteTakeover : HediffWithComps
    {
        public override bool Visible => false;

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta); // ticks the SeverityPerDay comp, applies its growth
            if (pawn == null || pawn.Dead || !pawn.IsHashIntervalTick(250, delta))
            {
                return;
            }
            if (Severity >= def.maxSeverity - 0.0001f)
            {
                SlimeCarrierUtility.ApplyCarrierGenes(pawn);
                pawn.health.RemoveHediff(this);
            }
        }
    }

    /// <summary>
    /// The slime carrier marker, present from Stage 1 (secret) through Stage 3 (turned).
    /// Sterile once turned (fertilityFactor 0 in the def's stage) and the identity every
    /// carrier carve-out keys off. Hidden while she passes as an ordinary woman;
    /// <see cref="revealed"/> flips it visible on the health tab when the genes land.
    /// Scribed, so a secret carrier stays secret across save/load.
    /// </summary>
    public class Hediff_ParasiteCarrier : Hediff
    {
        /// <summary>Whether the colony knows what she is. Scribed.</summary>
        public bool revealed;

        public override bool Visible => revealed;

        /// <summary>The pawn's own tooltip/inspect line, so a secret carrier's info
        /// panel doesn't leak the parasite to an observant player.</summary>
        public override string LabelInBrackets => revealed ? base.LabelInBrackets : null;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref revealed, "parasiteCarrierRevealed", false);
        }
    }
}
