using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// The slime carrier event, mirror of the nureonago visit with the rain stripped
    /// out. A travelling woman walks up to the colony and waits at its edge, smiling.
    /// She really was an ordinary woman once: she is generated as a plain baseliner with
    /// vanilla backstories, name, hair and looks - then carrier-ified post-generation
    /// (Momo + slime-gel genes, hidden carrier hediff). Nothing about her reads as a
    /// monster until the reveal.
    ///
    /// Unlike the nureonago she needs no rain to pass as human, so the only gates are:
    /// at least one bondable man in the colony (the parasite seeks men, like any slime),
    /// and no slime carrier already on the map. Talking to her and accepting or
    /// rejecting her reveals the truth; the flow follows the refugee-join chart:
    ///   Accept -> reveal, she joins. No automatic bonding.
    ///   Reject -> reveal, then the level check (IsekaiCompat.GetLevel), exactly like
    ///             the nureonago: carrier stronger than the talker -> she attacks;
    ///             otherwise she leaves. (Carriers ARE Momos, so she berserks.)
    /// Ignored, she simply leaves when her patience runs out (VisitDurationTicks).
    /// </summary>
    public class IncidentWorker_CarrierSlimeVisit : IncidentWorker
    {
        /// <summary>The pawn kind she spawns as: a disguised human woman.</summary>
        public const string CarrierKind = "PMM_CarrierSlimeRefugee";

        /// <summary>How often the visitor comp re-checks her wait/walk/leave state.</summary>
        public const int WaitCheckInterval = 250;

        /// <summary>How long she waits to be called over before giving up: ~18 in-game hours.</summary>
        public const int VisitDurationTicks = 45000;

        /// <summary>
        /// Gate: at least one bondable man in the colony, and no slime carrier (visiting
        /// or joined, secret or revealed) already on the map. No weather requirement:
        /// a parasite slime's disguise does not wash off. Runs on every storyteller
        /// tick, so the checks stay cheap.
        /// </summary>
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!(parms.target is Map map))
            {
                return false;
            }
            if (!HasBondableMan(map))
            {
                return false; // no unbonded man / protagonist to smile at
            }
            if (HasCarrier(map))
            {
                return false; // clamp: only one carrier visit at a time
            }
            return true;
        }

        /// <summary>True if any free colonist is a bondable man (unbonded male, or protagonist).</summary>
        private static bool HasBondableMan(Map map)
        {
            IReadOnlyList<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p != null && !p.Dead && ProjectMomo.TsugaiFormation.IsBondable(p))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if a slime carrier (the visitor, or any carrier including a
        /// joined colonist) is already on the map. She has no unique xenotype, so the
        /// carrier marker hediff is the scan key alongside her pawn kind.</summary>
        private static bool HasCarrier(Map map)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Dead)
                {
                    continue;
                }
                if (p.kindDef?.defName == CarrierKind || SlimeCarrierUtility.IsCarrier(p))
                {
                    return true;
                }
            }
            return false;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!CellFinder.TryFindRandomEdgeCellWith(
                    c => map.reachability.CanReachColony(c), map,
                    CellFinder.EdgeRoadChance_Ignore, out IntVec3 cell))
            {
                return false;
            }

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed(CarrierKind);
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
                // forceNoGear false: let the pawn kind's apparelTags/apparelMoney dress
                // her like a normal beggar (it has no weaponTags, so still no weapon).
                forceNoGear: false));

            // Seed the parasite (hidden marker, NO genes - her Bio tab stays clean) and
            // mask her real colonist backstories with "unknown" placeholders, the
            // nureonago disguise trick. The generation postfix has already attached the
            // visitor comp and carrier-ified her; both are idempotent, belt-and-braces.
            SlimeCarrierUtility.MakeCarrier(pawn, revealed: false);
            CarrierSlimeBackstories.Mask(pawn);

            // Factionless like a wandering stranger, not a wild creature. Clear the
            // faction FIRST, then the host faction that SetFaction just recorded, or the
            // vanilla guest branch would issue ExitMapBest (same trick as the nureonago).
            if (pawn.Faction != null)
            {
                pawn.SetFaction(null, null);
            }
            if (pawn.guest != null)
            {
                HostFactionRef(pawn.guest) = null;
            }

            GenSpawn.Spawn(pawn, cell, map);

            // The visitor brain: walk to the colony and loiter there, leave when her
            // patience runs out, and break the disguise if she is attacked. The postfix
            // attached a comp during generation; reuse it if so (PawnKindDef has no
            // comps field, so this cannot be declared in XML).
            CarrierSlimeVisitorComp comp = pawn.TryGetComp<CarrierSlimeVisitorComp>();
            if (comp == null)
            {
                comp = new CarrierSlimeVisitorComp();
                comp.parent = pawn;
                pawn.AllComps.Add(comp);
            }
            comp.Notify_Arrived();

            TaggedString letterText = "PMM_LetterCarrierArrive".Translate(pawn.Named("PAWN"))
                .AdjustedFor(pawn).CapitalizeFirst();
            SendStandardLetter(def.letterLabel, letterText, def.letterDef, parms, pawn);
            return true;
        }

        private static readonly AccessTools.FieldRef<Pawn_GuestTracker, Faction> HostFactionRef =
            AccessTools.FieldRefAccess<Pawn_GuestTracker, Faction>("hostFactionInt");
    }

    /// <summary>
    /// The disguise's backstory mask. Her real colonist backstories are generated by the
    /// pawn kind's filter (adulthood from the colonist pool) at spawn, then masked here
    /// with the "unknown" placeholders - the same trick the nureonago uses. On acceptance
    /// the mask is lifted: her snapshotted real stories are restored verbatim (not
    /// re-rolled), so the woman revealed is the woman who was always there. The snapshot
    /// lives on the visitor comp so it survives save/load.
    /// </summary>
    public static class CarrierSlimeBackstories
    {
        private static readonly AccessTools.FieldRef<Pawn_StoryTracker, BackstoryDef> ChildhoodRef =
            AccessTools.FieldRefAccess<Pawn_StoryTracker, BackstoryDef>("childhood");
        private static readonly AccessTools.FieldRef<Pawn_StoryTracker, BackstoryDef> AdulthoodRef =
            AccessTools.FieldRefAccess<Pawn_StoryTracker, BackstoryDef>("adulthood");

        /// <summary>Snapshot her real backstories onto the comp, then stamp "unknown".
        /// Idempotent: a second call must NOT re-snapshot, or it would capture the
        /// already-masked "unknown" placeholders and clobber the real stories (the
        /// postfix and the incident both call Mask, so this guard is what keeps the
        /// snapshot intact).</summary>
        public static void Mask(Pawn pawn)
        {
            if (pawn?.story == null)
            {
                return;
            }
            CarrierSlimeVisitorComp comp = pawn.TryGetComp<CarrierSlimeVisitorComp>();
            if (comp != null && comp.realChildhood == null)
            {
                // First mask only: capture her real generated stories before overwriting.
                comp.realChildhood = ChildhoodRef(pawn.story);
                comp.realAdulthood = AdulthoodRef(pawn.story);
            }
            BackstoryDef unknownChild = DefDatabase<BackstoryDef>.GetNamedSilentFail("PMM_UnknownChildhood");
            BackstoryDef unknownAdult = DefDatabase<BackstoryDef>.GetNamedSilentFail("PMM_UnknownAdulthood");
            if (unknownChild != null)
            {
                ChildhoodRef(pawn.story) = unknownChild;
            }
            if (unknownAdult != null)
            {
                AdulthoodRef(pawn.story) = unknownAdult;
            }
        }

        /// <summary>Restore her snapshotted real backstories (called on acceptance).</summary>
        public static void Unmask(Pawn pawn)
        {
            if (pawn?.story == null)
            {
                return;
            }
            CarrierSlimeVisitorComp comp = pawn.TryGetComp<CarrierSlimeVisitorComp>();
            if (comp?.realChildhood != null)
            {
                ChildhoodRef(pawn.story) = comp.realChildhood;
            }
            if (comp?.realAdulthood != null)
            {
                AdulthoodRef(pawn.story) = comp.realAdulthood;
            }
        }
    }

    /// <summary>
    /// The carrier visitor's brain, mirror of NureonagoVisitorComp: walk to a spot just
    /// outside the colony (the creepjoiner spot picker), loiter, and leave when her
    /// patience runs out instead of when the rain stops - a parasite slime keeps her
    /// host dry. The disguise breaks (into a berserk attack) if she is attacked. Only
    /// active while she is factionless and unrevealed; once she joins or is revealed,
    /// this stands down. Fully scribed: a secret carrier mid-visit survives save/load.
    /// </summary>
    public class CarrierSlimeVisitorComp : ThingComp
    {
        private bool waiting = true;
        private IntVec3 waitSpot = IntVec3.Invalid;
        private int departTick = -1;

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
        // JobGiver_CarrierSlimeWait) drives her approach and loiter; this comp only makes
        // her leave when her visit time runs out.
        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (!waiting || pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return;
            }
            if (!pawn.IsHashIntervalTick(IncidentWorker_CarrierSlimeVisit.WaitCheckInterval))
            {
                return; // only re-check the clock a few times a second
            }
            if (pawn.Map == null || pawn.Faction != null)
            {
                return; // joined / revealed: no longer our concern
            }

            if (departTick < 0)
            {
                // Dev spawn or a save from before the timer existed: start the clock now.
                departTick = Find.TickManager.TicksGame + IncidentWorker_CarrierSlimeVisit.VisitDurationTicks;
                return;
            }

            if (Find.TickManager.TicksGame >= departTick)
            {
                // Nobody called out to her in time: she moves on.
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
            departTick = Find.TickManager.TicksGame + IncidentWorker_CarrierSlimeVisit.VisitDurationTicks;
            TryPickWaitSpot();
        }

        /// <summary>Called by the attack patch: her cover is blown, so she flees. She has
        /// no slime/Momo genes yet (decision R2), so she cannot berserk - the parasite
        /// simply drives its host to escape with their secret intact.</summary>
        public void Notify_Attacked()
        {
            Pawn pawn = parent as Pawn;
            if (!waiting || pawn == null || pawn.Dead || pawn.Faction != null)
            {
                return;
            }
            waiting = false;
            Messages.Message(
                "PMM_CarrierDisguiseBroken".Translate(pawn.Named("PAWN")).AdjustedFor(pawn),
                pawn, MessageTypeDefOf.NegativeEvent, historical: false);
            pawn.mindState.duty = null;
            if (RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 spot))
            {
                Job leave = JobMaker.MakeJob(JobDefOf.Goto, spot);
                leave.exitMapOnArrival = true;
                pawn.jobs?.TryTakeOrderedJob(leave, JobTag.Misc);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref waiting, "carrierWaiting", true);
            Scribe_Values.Look(ref waitSpot, "carrierWaitSpot", IntVec3.Invalid);
            Scribe_Values.Look(ref departTick, "carrierDepartTick", -1);
            Scribe_Defs.Look(ref realChildhood, "carrierRealChildhood");
            Scribe_Defs.Look(ref realAdulthood, "carrierRealAdulthood");
        }

        /// <summary>Her real (pre-mask) backstories, restored on acceptance. Scribed.</summary>
        public BackstoryDef realChildhood;
        public BackstoryDef realAdulthood;
    }

    /// <summary>
    /// The disguised carrier's wait-and-approach behaviour, mirror of
    /// JobGiver_NureonagoWait: a JobGiver_Wander whose wander root is her comp's wait
    /// spot (travel toward it while far, small strolls once close - the
    /// creepjoiner/trader pattern with no lord/duty). Same two hard-won guards:
    /// maxDanger Deadly (any lower and every cell near a colony fails CanWanderToCell,
    /// handing her to the exit branch), and never return null while waiting (one null
    /// tick does the same). Returns null only once she is revealed or done waiting.
    /// </summary>
    public class JobGiver_CarrierSlimeWait : JobGiver_Wander
    {
        public JobGiver_CarrierSlimeWait()
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
            return pawn.TryGetComp<CarrierSlimeVisitorComp>()?.WaitSpot ?? IntVec3.Invalid;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            // Stand down once she is revealed (accepted/rejected/attacked) or no longer
            // waiting: null lets the think tree's other branches take over.
            CarrierSlimeVisitorComp comp = pawn.TryGetComp<CarrierSlimeVisitorComp>();
            if (comp == null || !comp.WaitSpot.IsValid || !CarrierSlimeDisguise.IsDisguised(pawn))
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

    /// <summary>Shared disguise check for the carrier patches: her pawn kind plus a
    /// still-secret carrier hediff. Once the truth is out (talk or takeover), every
    /// disguise mechanism stands down.</summary>
    public static class CarrierSlimeDisguise
    {
        public static bool IsDisguised(Pawn pawn)
        {
            return pawn != null &&
                   pawn.kindDef?.defName == IncidentWorker_CarrierSlimeVisit.CarrierKind &&
                   SlimeCarrierUtility.IsSecretCarrier(pawn);
        }
    }

    /// <summary>
    /// Break the disguise when the disguised traveller is attacked by the player, mirror
    /// of Patch_NureonagoDisguiseBreak. A postfix on <see cref="Thing.TakeDamage"/>: if
    /// the victim is a disguised carrier and the instigator is player-faction, reveal
    /// her and turn her berserk. Damage still applies normally.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_CarrierSlimeDisguiseBreak
    {
        public static void Postfix(Thing __instance, DamageInfo dinfo)
        {
            if (!(__instance is Pawn pawn) || pawn.Dead || !CarrierSlimeDisguise.IsDisguised(pawn))
            {
                return;
            }
            if (dinfo.Instigator == null || dinfo.Instigator.Faction != Faction.OfPlayer)
            {
                return; // only player violence breaks the disguise
            }
            pawn.TryGetComp<CarrierSlimeVisitorComp>()?.Notify_Attacked();
        }
    }

    /// <summary>
    /// The right-click "talk to the traveller" order on the disguised carrier, mirror of
    /// Patch_NureonagoFloatMenu. A postfix on <see cref="Pawn.GetFloatMenuOptions"/>:
    /// for a player-controlled colonist right-clicking a disguised carrier, add an
    /// option that queues the talk job.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetFloatMenuOptions))]
    public static class Patch_CarrierSlimeFloatMenu
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
            if (!CarrierSlimeDisguise.IsDisguised(__instance))
            {
                yield break;
            }
            if (!selPawn.CanReach(__instance, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption(
                    "PMM_TalkToCarrierNoPath".Translate(), null);
                yield break;
            }

            Pawn target = __instance;
            Pawn talker = selPawn;
            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption("PMM_TalkToCarrier".Translate(target.Named("PAWN")),
                    () => talker.jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("PMM_Job_TalkToCarrierSlime"), target))),
                talker, target);
        }
    }

    /// <summary>
    /// Walk up to the disguised traveller, then open the accept/reject dialog, mirror of
    /// JobDriver_TalkToNureonago.
    /// </summary>
    public class JobDriver_TalkToCarrierSlime : JobDriver
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

            Toil talk = ToilMaker.MakeToil("TalkToCarrierSlime");
            talk.initAction = () => CarrierSlimeDialogue.Open(pawn, Stranger);
            talk.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return talk;
        }
    }

    /// <summary>
    /// The accept/reject dialog and the refugee-join flow-chart outcomes, mirror of
    /// NureonagoDialogue. Carriers are Momos, so rejection runs the same level check:
    /// stronger than the talker, she attacks; otherwise she leaves.
    /// </summary>
    public static class CarrierSlimeDialogue
    {
        public static void Open(Pawn talker, Pawn stranger)
        {
            if (talker == null || stranger == null || stranger.Dead)
            {
                return;
            }
            var dialog = new Dialog_MessageBox(
                "PMM_CarrierDialogue".Translate(stranger.Named("PAWN"), talker.Named("TALKER"))
                    .AdjustedFor(stranger),
                buttonAText: "PMM_CarrierAccept".Translate(),
                buttonAAction: () => Accept(talker, stranger),
                buttonBText: "PMM_CarrierReject".Translate(),
                buttonBAction: () => Reject(talker, stranger),
                title: "PMM_CarrierDialogueTitle".Translate(),
                layer: WindowLayer.Dialog);
            Find.WindowStack.Add(dialog);
        }

        /// <summary>Accept: her disguise comes off - the "unknown" mask lifts to reveal
        /// her real colonist backstories, she joins, and the hidden ~2-day takeover that
        /// ends in the slime/Momo genes begins. She works as a normal colonist meanwhile.</summary>
        private static void Accept(Pawn talker, Pawn stranger)
        {
            CarrierSlimeBackstories.Unmask(stranger);
            SlimeCarrierUtility.BeginTakeover(stranger);

            // She moves in: become a colonist.
            stranger.SetFaction(Faction.OfPlayer, null);
        }

        /// <summary>
        /// Reject (decision R2): still an ordinary woman with no genes to betray her, she
        /// simply accepts the rejection and moves on. No reveal, no berserk - the parasite
        /// keeps its host and its secret.
        /// </summary>
        private static void Reject(Pawn talker, Pawn stranger)
        {
            if (RCellFinder.TryFindBestExitSpot(stranger, out IntVec3 spot))
            {
                Job leave = JobMaker.MakeJob(JobDefOf.Goto, spot);
                leave.exitMapOnArrival = true;
                stranger.jobs?.TryTakeOrderedJob(leave, JobTag.Misc);
            }
        }
    }
}
