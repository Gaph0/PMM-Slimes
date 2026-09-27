using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace PMM_SlimeFaction
{
    public class CompProperties_ParalyticTentacles : CompProperties
    {
        /// <summary>How far (in cells) the sea slime can strike with its tentacles.</summary>
        public float range = 6f;

        /// <summary>Cooldown between uses, in in-game hours.</summary>
        public float cooldownHours = 12f;

        /// <summary>Starting severity of the paralysis hediff (decays to 0 over 1 hour).</summary>
        public float severity = 1f;

        public CompProperties_ParalyticTentacles()
        {
            compClass = typeof(CompParalyticTentacles);
        }
    }

    /// <summary>
    /// Gives a player-owned sea slime a "paralytic tentacles" gizmo: tentacles emerge
    /// from beneath her bell and inject a paralytic poison into a nearby target,
    /// slowing it to a crawl for 1 in-game hour (the PMM_Hediff_SeaParalytic hediff).
    /// On the sea race def (Race_SlimeMamono.xml), so every sea slime has it; the gizmo
    /// only shows for player-faction pawns. 12-hour cooldown.
    /// </summary>
    public class CompParalyticTentacles : ThingComp
    {
        private int lastUseTick = -999999;

        public CompProperties_ParalyticTentacles Props => (CompProperties_ParalyticTentacles)props;

        private int CooldownTicks => (int)(Props.cooldownHours * GenDate.TicksPerHour);

        private bool OnCooldown => Find.TickManager.TicksGame < lastUseTick + CooldownTicks;

        private int CooldownTicksRemaining => (lastUseTick + CooldownTicks) - Find.TickManager.TicksGame;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!(parent is Pawn pawn) || pawn.Faction != Faction.OfPlayer ||
                pawn.Dead || !pawn.Spawned)
            {
                yield break;
            }

            var command = new Command_Action
            {
                defaultLabel = "Paralytic tentacles",
                defaultDesc = "Order the sea slime to lash a nearby creature with the tentacles beneath " +
                              "her bell, injecting a paralytic poison that slows it to a crawl for an " +
                              $"hour. Usable once every {Props.cooldownHours:F0} hours.",
                icon = parent.def.uiIcon,
                action = delegate { StartTargeting(pawn); }
            };
            if (OnCooldown)
            {
                command.Disable($"Recharging ({CooldownTicksRemaining.ToStringTicksToPeriod()}).");
            }
            yield return command;
        }

        /// <summary>Begin targeting: pick a pawn within reach to inject with the paralytic.</summary>
        private void StartTargeting(Pawn pawn)
        {
            var targetingParameters = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetHumans = true,
                canTargetAnimals = true,
                canTargetSelf = false,
                canTargetBuildings = false,
                canTargetItems = false,
                canTargetLocations = false,
                mapObjectTargetsMustBeAutoAttackable = false,
                validator = target =>
                    target.HasThing && target.Thing is Pawn t && t != pawn && !t.Dead &&
                    pawn.Position.DistanceTo(t.Position) <= Props.range
            };

            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                if (target.HasThing && target.Thing is Pawn victim)
                {
                    Inject(pawn, victim);
                }
            }, caster: pawn, actionWhenFinished: null, mouseAttachment: null, requiresCastedSelected: true);
        }

        /// <summary>Strike the target and apply (or top up) the paralytic poison.</summary>
        private void Inject(Pawn pawn, Pawn victim)
        {
            if (victim?.health == null || victim.Dead)
            {
                return;
            }

            HediffDef def = SlimeDefOf.PMM_Hediff_SeaParalytic;
            Hediff hediff = victim.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(def, victim);
                hediff.Severity = Props.severity;
                victim.health.AddHediff(hediff);
            }
            else
            {
                // Top back up to full duration rather than stacking severity.
                hediff.Severity = Props.severity;
            }

            lastUseTick = Find.TickManager.TicksGame;
            SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(victim.Position, victim.Map));
            MoteMaker.ThrowText(victim.DrawPos, victim.Map, "Paralysed!", 1.9f);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastUseTick, "lastUseTick", -999999);
        }
    }
}
