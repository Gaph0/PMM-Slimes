using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// The nureonago event. A strange woman walks out of the rain and settles just
    /// outside the colony, smiling. She is a nureonago (a Zipangu slime) in disguise: a
    /// plain human baseliner with "unknown" backstories, nothing about her reading as a monster.
    /// She approaches the way vanilla creepjoiners and traders do: travel to a spot just
    /// outside the colony, then loiter there.
    ///
    /// Only fires during rainy weather (Rain / RainyThunderstorm / Odyssey's
    /// TorrentialRain) while the colony has at least one bondable man (an unbonded male
    /// of age, or a protagonist - TsugaiFormation.IsBondable), and never while a nureonago
    /// is already on the map. Talking to her and either accepting or rejecting her
    /// reveals what she really is; the flow then follows the refugee-join flow chart:
    ///   Accept -> reveal, she joins. No automatic bonding.
    ///   Reject -> reveal, then a level check (IsekaiCompat.GetLevel):
    ///             mamono level > pawn level -> she attacks; otherwise she leaves.
    /// Ignored, she simply leaves when the rain stops.
    /// </summary>
    public class IncidentWorker_NureonagoVisit : IncidentWorker
    {
        /// <summary>The pawn kind she spawns as: a disguised human woman.</summary>
        private const string RefugeeKind = "PMM_Slime_Nureonago";

        /// <summary>How often the visitor comp re-checks her wait/walk/leave state.</summary>
        public const int WaitCheckInterval = 250;

        /// <summary>Rainy weathers she shows herself in. TorrentialRain is Odyssey-only.</summary>
        private static readonly string[] RainWeathers = { "Rain", "RainyThunderstorm", "TorrentialRain" };

        /// <summary>
        /// Gate: rainy weather, at least one bondable man in the colony, and no nureonago
        /// already present. Runs on every storyteller tick, so the checks are cheap.
        /// </summary>
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!(parms.target is Map map))
            {
                return false;
            }
            if (!IsRaining(map))
            {
                return false; // she only shows herself in the rain
            }
            if (!HasBondableMan(map))
            {
                return false; // no unbonded man / protagonist to smile at
            }
            if (HasNureonago(map))
            {
                return false; // clamp: only one nureonago visit at a time
            }
            return true;
        }

        public static bool IsRaining(Map map)
        {
            WeatherDef cur = map?.weatherManager?.curWeather;
            if (cur == null)
            {
                return false;
            }
            for (int i = 0; i < RainWeathers.Length; i++)
            {
                if (cur.defName == RainWeathers[i])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if any free colonist is a bondable man (unbonded male, or protagonist).</summary>
        private static bool HasBondableMan(Map map)
        {
            IReadOnlyList<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p != null && !p.Dead && ProjectMamono.TsugaiFormation.IsBondable(p))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if a nureonago (disguised or revealed) is already on the map.</summary>
        private static bool HasNureonago(Map map)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Dead)
                {
                    continue;
                }
                if (p.kindDef?.defName == RefugeeKind ||
                    p.genes?.Xenotype?.defName == NureonagoXenotype)
                {
                    return true;
                }
            }
            return false;
        }

        public const string NureonagoXenotype = "PMM_Slime_Nureonago";

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!CellFinder.TryFindRandomEdgeCellWith(
                    c => map.reachability.CanReachColony(c), map,
                    CellFinder.EdgeRoadChance_Ignore, out IntVec3 cell))
            {
                return false;
            }

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed(RefugeeKind);
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind, Faction.OfPlayer, PawnGenerationContext.NonPlayer, map.Tile,
                forceGenerateNewPawn: false, allowDead: false, allowDowned: false,
                canGeneratePawnRelations: true, mustBeCapableOfViolence: false,
                colonistRelationChanceFactor: 1f, forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true, allowPregnant: true, allowFood: true, allowAddictions: false,
                inhabitant: false, certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false,
                biocodeWeaponChance: 0f, biocodeApparelChance: 0f,
                extraPawnForExtraRelationChance: null, relationWithExtraPawnChanceFactor: 1f,
                validatorPreGear: null, validatorPostGear: null, forcedTraits: null,
                prohibitedTraits: null, minChanceToRedressWorldPawn: null,
                fixedBiologicalAge: null, fixedChronologicalAge: null, fixedGender: Gender.Female,
                fixedLastName: null, fixedBirthName: null, fixedTitle: null,
                fixedIdeo: null, forceNoIdeo: false, forceNoBackstory: false,
                forbidAnyTitle: false, forceDead: false, forcedXenogenes: null,
                forcedEndogenes: null, forcedXenotype: null, forcedCustomXenotype: null,
                allowedXenotypes: null, forceBaselinerChance: 0f,
                developmentalStages: DevelopmentalStage.Adult,
                forceNoGear: true));

            // Nobody knows where she came from.
            SetUnknownBackstories(pawn);

            // Factionless like a wandering stranger, not a wild creature. Two things would
            // otherwise make the vanilla guest branch (Humanlike tree) issue ExitMapBest:
            //   - HostFaction: generation assigns a host faction, which is exactly what
            //     ThinkNode_ConditionalGuest checks. Clear it so she is NOT a guest.
            //   - SetFaction: RimWorld sets HostFaction to the OLD faction on faction change,
            //     so clear the faction FIRST, then clear the host faction it just set.
            if (pawn.Faction != null)
            {
                pawn.SetFaction(null, null);
            }
            if (pawn.guest != null)
            {
                HostFactionRef(pawn.guest) = null;
            }

            GenSpawn.Spawn(pawn, cell, map);

            // The visitor brain: walk to the colony and loiter there, leave when the
            // rain stops, and break the disguise if she is attacked. PawnKindDef has no
            // comps field, so the comp is attached here at spawn rather than declared in XML.
            NureonagoVisitorComp comp = new NureonagoVisitorComp();
            comp.parent = pawn;
            pawn.AllComps.Add(comp);
            comp.Notify_Arrived();

            TaggedString letterText = "PMM_LetterNureonagoArrive".Translate(pawn.Named("PAWN"))
                .AdjustedFor(pawn).CapitalizeFirst();
            SendStandardLetter(def.letterLabel, letterText, def.letterDef, parms, pawn);
            return true;
        }

        /// <summary>
        /// Stamp both backstory slots with the "unknown" stories. These are private fields,
        /// set here (post-generation) via the same FieldRef pattern Patch_SlimeBackstory uses.
        /// </summary>
        private static void SetUnknownBackstories(Pawn pawn)
        {
            if (pawn?.story == null)
            {
                return;
            }
            BackstoryDef childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail("PMM_UnknownChildhood");
            BackstoryDef adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail("PMM_UnknownAdulthood");
            if (childhood != null)
            {
                ChildhoodRef(pawn.story) = childhood;
            }
            if (adulthood != null)
            {
                AdulthoodRef(pawn.story) = adulthood;
            }
        }

        private static readonly AccessTools.FieldRef<Pawn_StoryTracker, BackstoryDef> ChildhoodRef =
            AccessTools.FieldRefAccess<Pawn_StoryTracker, BackstoryDef>("childhood");
        private static readonly AccessTools.FieldRef<Pawn_StoryTracker, BackstoryDef> AdulthoodRef =
            AccessTools.FieldRefAccess<Pawn_StoryTracker, BackstoryDef>("adulthood");
        private static readonly AccessTools.FieldRef<Pawn_GuestTracker, Faction> HostFactionRef =
            AccessTools.FieldRefAccess<Pawn_GuestTracker, Faction>("hostFactionInt");
    }

    /// <summary>
    /// Reveals the disguised woman as a nureonago: swaps her xenotype to the slime, moves
    /// her to the slime race (so her info card and butcher yields match), replaces the
    /// "unknown" backstories with the slime life story, dirties her graphics, and sends
    /// the reveal letter. Shared by the talk outcome (accept/reject) and the attack break.
    /// </summary>
    public static class NureonagoReveal
    {
        private static readonly AccessTools.FieldRef<Pawn_StoryTracker, BackstoryDef> ChildhoodRef =
            AccessTools.FieldRefAccess<Pawn_StoryTracker, BackstoryDef>("childhood");
        private static readonly AccessTools.FieldRef<Pawn_StoryTracker, BackstoryDef> AdulthoodRef =
            AccessTools.FieldRefAccess<Pawn_StoryTracker, BackstoryDef>("adulthood");

        public static bool IsDisguised(Pawn pawn)
        {
            return pawn != null && pawn.kindDef?.defName == "PMM_Slime_Nureonago" &&
                   pawn.genes?.Xenotype?.defName != IncidentWorker_NureonagoVisit.NureonagoXenotype;
        }

        /// <summary>Swap her into the nureonago xenotype and send the reveal letter.</summary>
        public static void Reveal(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.genes == null)
            {
                return;
            }

            XenotypeDef xenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(
                IncidentWorker_NureonagoVisit.NureonagoXenotype);
            if (xenotype == null)
            {
                return;
            }

            // Add the slime genes she lacks as endogenes (her old skin colour is removed
            // first so it can't conflict), then stamp the xenotype identity.
            for (int i = 0; i < xenotype.genes.Count; i++)
            {
                GeneDef gene = xenotype.genes[i];
                RemoveConflictingGenes(pawn, gene);
                if (!pawn.genes.HasActiveGene(gene))
                {
                    pawn.genes.AddGene(gene, xenogene: false);
                }
            }
            pawn.genes.SetXenotypeDirect(xenotype);

            // Move her to the slime race so the info card shows slime jelly, not human meat.
            if (pawn.kindDef != null && pawn.kindDef.race != SlimeDefOf.PMM_Race_SlimeMamono)
            {
                pawn.def = SlimeDefOf.PMM_Race_SlimeMamono;
            }

            // Now that she is known to be a slime, she gets the slime life story.
            SwapToSlimeBackstories(pawn);

            if (pawn.Spawned && pawn.Drawer?.renderer != null)
            {
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }

            Find.LetterStack.ReceiveLetter(
                "PMM_LetterLabelNureonagoReveal".Translate(),
                "PMM_LetterNureonagoReveal".Translate(pawn.Named("PAWN")).AdjustedFor(pawn),
                LetterDefOf.NeutralEvent, pawn);
        }

        /// <summary>Remove genes that conflict with an incoming gene (e.g. her old skin colour).</summary>
        private static void RemoveConflictingGenes(Pawn pawn, GeneDef incoming)
        {
            if (incoming?.exclusionTags == null || pawn.genes == null)
            {
                return;
            }
            // Collect first: removing while enumerating GenesListForReading is unsafe.
            List<Gene> toRemove = null;
            foreach (Gene existing in pawn.genes.GenesListForReading)
            {
                if (existing?.def?.exclusionTags == null)
                {
                    continue;
                }
                foreach (string tag in incoming.exclusionTags)
                {
                    if (existing.def.exclusionTags.Contains(tag))
                    {
                        (toRemove ?? (toRemove = new List<Gene>())).Add(existing);
                        break;
                    }
                }
            }
            if (toRemove != null)
            {
                foreach (Gene gene in toRemove)
                {
                    pawn.genes.RemoveGene(gene);
                }
            }
        }

        /// <summary>Replace the "unknown" stories with the slime childhood/adulthood.</summary>
        private static void SwapToSlimeBackstories(Pawn pawn)
        {
            if (pawn?.story == null)
            {
                return;
            }
            BackstoryDef childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail("PMM_SlimeChild");
            BackstoryDef adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail("PMM_SlimeAdult");
            if (childhood != null)
            {
                ChildhoodRef(pawn.story) = childhood;
            }
            if (adulthood != null)
            {
                AdulthoodRef(pawn.story) = adulthood;
            }
        }
    }

    /// <summary>
    /// The nureonago visitor's brain, attached to the refugee pawn kind. Mirrors how
    /// vanilla creepjoiners and traders approach a colony: she walks to a spot just
    /// outside it (RCellFinder.TryFindRandomSpotJustOutsideColony - the exact spot picker
    /// CreepJoinerUtility.GenerateAndSpawn uses), then loiters there with short wander
    /// jobs (the WanderClose duty's JobGiver_WanderNearDutyLocation pattern). She leaves
    /// when the rain stops, and the disguise breaks (into a berserk attack) if she is
    /// attacked. Only active while she is factionless and unrevealed; once she joins or
    /// is revealed, this stands down.
    ///
    /// Duties only run for pawns in a lord (ThinkNode_Duty errors with no lord), so
    /// instead of the vanilla LordToil_Travel / LordToil_WanderNearby pair this drives
    /// the same behaviour directly with jobs.
    /// </summary>
    public class NureonagoVisitorComp : ThingComp
    {
        private bool waiting = true;
        private IntVec3 waitSpot = IntVec3.Invalid;

        /// <summary>Her wait spot for the wait job giver; Invalid once she has left/joined.</summary>
        public IntVec3 WaitSpot
        {
            get
            {
                if (!waiting)
                {
                    return IntVec3.Invalid;
                }
                if (!waitSpot.IsValid)
                {
                    // Recompute lazily: PostExposeData (a load-mode scribe with no saved
                    // value) resets waitSpot to Invalid, which would otherwise strand her.
                    TryPickWaitSpot();
                }
                return waitSpot;
            }
        }

        /// <summary>Pick a wait spot just outside the colony (the creepjoiner spot picker).</summary>
        private void TryPickWaitSpot()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Map == null)
            {
                return;
            }
            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(pawn, out waitSpot))
            {
                waitSpot = pawn.Position; // no colony spot found: wait where she stands
            }
        }

        // Pawns never receive CompTickRare: Pawn only implements TickInterval (via
        // ThingWithComps.Tick), which drives CompTick on each comp. The think tree (via
        // JobGiver_NureonagoWait) drives her approach and loiter; this comp only makes
        // her leave when the rain stops. Gated to every WaitCheckInterval ticks with a
        // hash-interval so the weather check stays cheap.
        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (!waiting || pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return;
            }
            if (!pawn.IsHashIntervalTick(IncidentWorker_NureonagoVisit.WaitCheckInterval))
            {
                return; // only re-check the weather a few times a second
            }
            if (pawn.Map == null || pawn.Faction != null)
            {
                return; // joined / revealed: no longer our concern
            }

            if (!IncidentWorker_NureonagoVisit.IsRaining(pawn.Map))
            {
                // The rain has stopped and no one called out to her: she slips away.
                waiting = false;
                pawn.mindState.duty = null;
                if (RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 spot))
                {
                    Job leave = JobMaker.MakeJob(JobDefOf.Goto, spot);
                    leave.exitMapOnArrival = true;
                    pawn.jobs?.TryTakeOrderedJob(leave, JobTag.Misc);
                }
            }
        }

        /// <summary>Called by the incident once she is spawned, to begin her wait.</summary>
        public void Notify_Arrived()
        {
            waiting = true;
            TryPickWaitSpot();
        }

        /// <summary>Called by the attack patch: break the disguise and fight back.</summary>
        public void Notify_Attacked()
        {
            Pawn pawn = parent as Pawn;
            if (!waiting || pawn == null || pawn.Dead || pawn.Faction != null)
            {
                return;
            }
            waiting = false;
            NureonagoReveal.Reveal(pawn);
            pawn.mindState?.mentalStateHandler?.TryStartMentalState(
                ProjectMamono.ProjectMamono_DefOf.ProjectMamono_EssenceBerserk,
                reason: "PMM_NureonagoDisguiseBroken".Translate(pawn.Named("PAWN")),
                forceWake: true);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref waiting, "nureonagoWaiting", true);
            Scribe_Values.Look(ref waitSpot, "nureonagoWaitSpot", IntVec3.Invalid);
        }
    }

    /// <summary>
    /// The disguised woman's wait-and-approach behaviour, driven by the think tree rather
    /// than by the comp issuing jobs. A <see cref="JobGiver_Wander"/> whose wander root is
    /// her comp's wait spot: while she is far from it, JobGiver_Wander keeps issuing
    /// travel-toward-the-root Goto jobs (its locomotionUrgencyOutsideRadius); once close,
    /// it issues the small GotoWander strolls around the spot. This is exactly the
    /// creepjoiner/trader pattern (LordToil_Travel into LordToil_WanderNearby) expressed
    /// as a single duty-free job giver, because she has no lord to hold a duty.
    ///
    /// Two failure guards, both learned the hard way:
    ///  - maxDanger must be Deadly, not None: CanWanderToCell rejects cells with a known
    ///    danger rating above maxDanger, and the area near a colony (colonists, turrets)
    ///    almost always rates Some danger. With None every wander cell fails, the giver
    ///    returns null, and the think tree's non-colonist backup (JobGiver_ExitMapBest)
    ///    marches her off the map. Vanilla wander always uses Deadly.
    ///  - The giver must never return null while she is waiting: one null tick hands her
    ///    to that same exit branch. If the base wander fails for any reason, fall back to
    ///    wandering where she stands, then to a short wait.
    /// Returns null only once she is revealed or done waiting, so the colonist / exit
    /// branches can take over.
    /// </summary>
    public class JobGiver_NureonagoWait : JobGiver_Wander
    {
        public JobGiver_NureonagoWait()
        {
            wanderRadius = 3f;                          // the WanderClose duty's radius
            ticksBetweenWandersRange = new IntRange(120, 240);
            locomotionUrgency = LocomotionUrgency.Walk;
            locomotionUrgencyOutsideRadius = LocomotionUrgency.Walk;
            maxDanger = Danger.Deadly;                  // vanilla wander danger level
        }

        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            // Invalid once she is done waiting (left/joined).
            return pawn.TryGetComp<NureonagoVisitorComp>()?.WaitSpot ?? IntVec3.Invalid;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            // Stand down once she is revealed (accepted/rejected/attacked) or no longer
            // waiting: null lets the think tree's other branches take over (colonist
            // behaviour after joining, the exit branch after rejection/rain). WaitSpot
            // self-heals (recomputes) while she waits, so it is valid here.
            NureonagoVisitorComp comp = pawn.TryGetComp<NureonagoVisitorComp>();
            if (comp == null || !comp.WaitSpot.IsValid || !NureonagoReveal.IsDisguised(pawn))
            {
                return null;
            }

            Job job = base.TryGiveJob(pawn);
            if (job != null)
            {
                return job;
            }

            // Never return null while she waits (see the class notes): wander where she
            // stands instead, or just wait in place if even that fails.
            IntVec3 dest = RCellFinder.RandomWanderDestFor(pawn, pawn.Position, 6f, null, Danger.Deadly);
            if (dest.IsValid)
            {
                Job wander = JobMaker.MakeJob(JobDefOf.GotoWander, dest);
                wander.locomotionUrgency = LocomotionUrgency.Walk;
                return wander;
            }
            Job wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = 120;
            wait.checkOverrideOnExpire = true;
            return wait;
        }
    }

    /// <summary>
    /// Break the disguise when the disguised woman is attacked by the player. A postfix on
    /// <see cref="Thing.TakeDamage"/>: if the victim is a disguised nureonago and the
    /// instigator is player-faction, reveal her and turn her berserk. Damage still applies
    /// normally (the postfix does not skip the original).
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_NureonagoDisguiseBreak
    {
        public static void Postfix(Thing __instance, DamageInfo dinfo)
        {
            if (!(__instance is Pawn pawn) || pawn.Dead || !NureonagoReveal.IsDisguised(pawn))
            {
                return;
            }
            if (dinfo.Instigator == null || dinfo.Instigator.Faction != Faction.OfPlayer)
            {
                return; // only player violence breaks the disguise
            }
            pawn.TryGetComp<NureonagoVisitorComp>()?.Notify_Attacked();
        }
    }

    /// <summary>
    /// The right-click "talk to the stranger" order on the disguised woman. A postfix on
    /// <see cref="Pawn.GetFloatMenuOptions"/>: for a player-controlled colonist
    /// right-clicking a disguised nureonago, add an option that queues the talk job.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetFloatMenuOptions))]
    public static class Patch_NureonagoFloatMenu
    {
        public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> __result,
            Pawn __instance, Pawn selPawn)
        {
            foreach (FloatMenuOption option in __result)
            {
                yield return option;
            }

            if (selPawn == null || !selPawn.IsColonistPlayerControlled || selPawn == __instance)
            {
                yield break;
            }
            if (!NureonagoReveal.IsDisguised(__instance))
            {
                yield break;
            }
            if (!selPawn.CanReach(__instance, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption(
                    "PMM_TalkToNureonagoNoPath".Translate(), null);
                yield break;
            }

            Pawn target = __instance;
            Pawn talker = selPawn;
            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption("PMM_TalkToNureonago".Translate(target.Named("PAWN")),
                    () => talker.jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("PMM_Job_TalkToNureonago"), target))),
                talker, target);
        }
    }

    /// <summary>
    /// Walk up to the disguised woman, then open the accept/reject dialog. Kept short
    /// (she is right there in the rain); the choice itself drives the outcome.
    /// </summary>
    public class JobDriver_TalkToNureonago : JobDriver
    {
        private Pawn Stranger => (Pawn)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Stranger == null || Stranger.Dead || Stranger.Faction != null);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil talk = ToilMaker.MakeToil("TalkToNureonago");
            talk.initAction = () => NureonagoDialogue.Open(pawn, Stranger);
            talk.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return talk;
        }
    }

    /// <summary>
    /// The accept/reject dialog and the refugee-join flow-chart outcomes.
    /// </summary>
    public static class NureonagoDialogue
    {
        public static void Open(Pawn talker, Pawn stranger)
        {
            if (talker == null || stranger == null || stranger.Dead)
            {
                return;
            }
            var dialog = new Dialog_MessageBox(
                "PMM_NureonagoDialogue".Translate(stranger.Named("PAWN"), talker.Named("TALKER"))
                    .AdjustedFor(stranger),
                buttonAText: "PMM_NureonagoAccept".Translate(),
                buttonAAction: () => Accept(talker, stranger),
                buttonBText: "PMM_NureonagoReject".Translate(),
                buttonBAction: () => Reject(talker, stranger),
                title: "PMM_NureonagoDialogueTitle".Translate(),
                layer: WindowLayer.Dialog);
            Find.WindowStack.Add(dialog);
        }

        /// <summary>Accept: reveal and she joins. No automatic bonding.</summary>
        private static void Accept(Pawn talker, Pawn stranger)
        {
            NureonagoReveal.Reveal(stranger);

            // She moves in: become a colonist.
            stranger.SetFaction(Faction.OfPlayer, null);
        }

        /// <summary>
        /// Reject: reveal, then the level check. A mamono stronger than the talker attacks;
        /// otherwise she accepts the rejection and leaves. The talker being a protagonist
        /// does NOT exempt him from the roll.
        /// </summary>
        private static void Reject(Pawn talker, Pawn stranger)
        {
            NureonagoReveal.Reveal(stranger);

            int mamonoLevel = ProjectMamono.IsekaiCompat.GetLevel(stranger);
            int pawnLevel = ProjectMamono.IsekaiCompat.GetLevel(talker);

            if (mamonoLevel > pawnLevel)
            {
                // Spurned and stronger: she attacks.
                stranger.mindState?.mentalStateHandler?.TryStartMentalState(
                    ProjectMamono.ProjectMamono_DefOf.ProjectMamono_EssenceBerserk,
                    reason: "PMM_NureonagoScorned".Translate(stranger.Named("PAWN")),
                    forceWake: true);
            }
            else
            {
                // Outmatched (or evenly matched): she leaves.
                if (RCellFinder.TryFindBestExitSpot(stranger, out IntVec3 spot))
                {
                    Job leave = JobMaker.MakeJob(JobDefOf.Goto, spot);
                    leave.exitMapOnArrival = true;
                    stranger.jobs?.TryTakeOrderedJob(leave, JobTag.Misc);
                }
            }
        }
    }
}
