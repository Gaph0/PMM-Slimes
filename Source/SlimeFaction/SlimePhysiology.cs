using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// Slimes ooze slime, never trash. A full replacement for the vanilla ambient-filth
    /// branch: keeps the FilthRate gate (4x for slimes, from the gene), then always drops
    /// <see cref="ThingDefOf.Filth_Slime"/> - vanilla would drop the terrain filth 66% of
    /// the time and <c>Filth_Trash</c> the other 34%. Runs as a prefix that skips the
    /// original for slime pawns only.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_FilthTracker), nameof(Pawn_FilthTracker.Notify_EnteredNewCell))]
    public static class Patch_FilthTracker_Slime
    {
        private static readonly AccessTools.FieldRef<Pawn_FilthTracker, Pawn> PawnRef =
            AccessTools.FieldRefAccess<Pawn_FilthTracker, Pawn>("pawn");

        // AdditionalFilthSourceFlags getter is public-but-hidden and FilthMonitor is internal;
        // reach both through reflection.
        private static readonly System.Reflection.PropertyInfo AdditionalFlagsProp =
            AccessTools.Property(typeof(Pawn_FilthTracker), "AdditionalFilthSourceFlags");
        private static readonly System.Reflection.MethodInfo NotifyHumanFilth =
            AccessTools.Method("RimWorld.FilthMonitor:Notify_FilthHumanGenerated");

        public static bool Prefix(Pawn_FilthTracker __instance)
        {
            Pawn pawn = PawnRef(__instance);
            if (pawn == null || !pawn.RaceProps.Humanlike || !Gene_SlimeGel.IsSlime(pawn))
            {
                return true; // not a slime: vanilla logic untouched
            }

            // Same gate vanilla applies to every pawn: filth drops on roughly
            // FilthRate * 0.5% of entered cells (FilthRate 4 for slimes).
            if (Rand.Value < pawn.GetStatValue(StatDefOf.FilthRate) * 0.005f)
            {
                var flags = (FilthSourceFlags)(AdditionalFlagsProp?.GetValue(__instance) ?? FilthSourceFlags.None);
                FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, ThingDefOf.Filth_Slime, 1, flags, true);
                NotifyHumanFilth?.Invoke(null, null);
            }
            return false; // handled; skip vanilla so no terrain filth or trash is dropped
        }
    }

    /// <summary>
    /// Slimes have no bones to break and no skin to cut: every wound they suffer is a
    /// bruise. A prefix on <see cref="Thing.TakeDamage"/> - which Pawn does not override,
    /// so this single point catches all damage before armour and damage workers run -
    /// that rewrites the incoming <see cref="DamageInfo"/> in place for slime targets:
    /// the damage def becomes Blunt, and hits aimed at solid parts (bones are the only
    /// solid body parts) are retargeted to the nearest non-solid ancestor. Editing in
    /// place keeps every other flag (amount, armour penetration, instigator, executions,
    /// propagation) intact.
    ///
    /// Why Blunt and not Crush: HealthUtility.GetHediffDefFromDamage picks the wound
    /// hediff as hediffSkin for skin-covered parts, then hediffSolid for solid parts,
    /// then the plain hediff as fallback. Vanilla Crush's hediffSkin is Cut (a crushing
    /// blow tears skin), so converting to Crush made every flesh wound a cut - the exact
    /// opposite of this rule. Blunt's hediffSkin is Bruise, which is what we want. Blunt
    /// also brings the vanilla DamageWorker_Blunt extras (stun chance on heavy core-part
    /// hits, chance of inner-part damage) - the same profile as fists and clubs.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_SlimeTakeDamage
    {
        // DamageInfo is a STRUCT, so an instance FieldRefAccess<DamageInfo, ...> throws
        // ("T must not be a value type"). The def field is private; edit it via a cached
        // FieldInfo with GetValueDirect/SetValueDirect on __makeref, which works on value
        // types passed by ref. (The hit-part has a public SetHitPart, so only the def
        // needs this treatment.)
        private static readonly System.Reflection.FieldInfo DefField =
            AccessTools.Field(typeof(DamageInfo), "defInt");

        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (!(__instance is Pawn pawn) || !Gene_SlimeGel.IsSlime(pawn))
            {
                return;
            }

            // Any external violence lands as a bruise: bullets, cuts, burns, bites, all of it.
            // Blunt, not Crush: GetHediffDefFromDamage prefers hediffSkin on skin-covered
            // parts, and Crush's hediffSkin is Cut - Blunt's is Bruise.
            if (dinfo.Def != DamageDefOf.Blunt && dinfo.Def.ExternalViolenceFor(pawn))
            {
                DefField.SetValueDirect(__makeref(dinfo), DamageDefOf.Blunt);
            }

            // No bones: retarget hits on solid parts (only bones are solid) to flesh.
            // BodyPartDef.IsSolid is an instance method taking the record (parts can be
            // de-solidified by hediffs, e.g. bionics); passing null hediffs gives the
            // definition-level answer, which is what we want for the bone check.
            BodyPartRecord part = dinfo.HitPart;
            if (part != null && part.def.IsSolid(part, null))
            {
                BodyPartRecord current = part;
                while (current != null && current.def.IsSolid(current, null))
                {
                    current = current.parent;
                }
                if (current == null)
                {
                    // Whole part tree is solid (should never happen for humans): fall back
                    // to the torso/core part rather than a bone.
                    current = pawn.RaceProps?.body?.corePart;
                }
                dinfo.SetHitPart(current);
            }
        }
    }

    /// <summary>
    /// Butchering a slime yields only slime jelly - no meat and no skin. A postfix on
    /// <see cref="Pawn.ButcherProducts"/> replaces the result wholesale for slimes; the
    /// corpse path delegates to the inner pawn, so this covers corpses too.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ButcherProducts))]
    public static class Patch_SlimeButcherProducts
    {
        public static void Postfix(Pawn __instance, Pawn butcher, float efficiency,
            ref IEnumerable<Thing> __result)
        {
            if (!Gene_SlimeGel.IsSlime(__instance))
            {
                return;
            }

            // A slime carrier is still human flesh and skin under the slime's influence:
            // let vanilla butchery run (she kept her Human race, so Meat_Human and
            // Leather_Human fall out of the normal path). No jelly from her, ever.
            if (SlimeCarrierUtility.IsCarrier(__instance))
            {
                return;
            }

            // Yield = MeatAmount stat × butcher efficiency. MeatAmount is base 20 on the
            // slime race (scaled by body size and butcher-yield stat parts), so a healthy
            // adult at full efficiency drops ~20 jelly, and a careless or low-skill
            // butcher gets less - matching the meat stat row/tooltip shown in-game.
            int count = Mathf.Max(1, GenMath.RoundRandom(__instance.GetStatValue(StatDefOf.MeatAmount) * efficiency));
            Thing jelly = ThingMaker.MakeThing(JellyFor(__instance));
            jelly.stackCount = count;
            __result = new List<Thing> { jelly };
        }

        /// <summary>A slime yields the jelly matching its xenotype (blue/bubble/red/dark);
        /// anything else with the gene (e.g. a slime-gene-transformed human, or a custom
        /// xenotype) yields the basic blue slime jelly.</summary>
        private static ThingDef JellyFor(Pawn pawn)
        {
            return SlimeRaces.JellyFor(pawn?.genes?.Xenotype?.defName);
        }
    }

    /// <summary>
    /// Slimes are living water: standing in the rain doesn't bother them. A prefix on
    /// Pawn_MindState.CanGainGainThoughtNow - the gate MindStateTickInterval runs every
    /// 120 ticks before granting the current weather's weatherThought (SoakingWet for
    /// rain and thunderstorms) - that refuses the gain for slime pawns and scrubs any
    /// soaking-wet memory they already hold, so the moodlet clears immediately instead
    /// of lingering for its 0.1-day expiry. The method is private, so it is patched by
    /// name rather than with nameof.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_MindState), "CanGainGainThoughtNow")]
    public static class Patch_SlimeSoakingWet
    {
        private static readonly AccessTools.FieldRef<Pawn_MindState, Pawn> PawnRef =
            AccessTools.FieldRefAccess<Pawn_MindState, Pawn>("pawn");

        public static bool Prefix(Pawn_MindState __instance, ThoughtDef thought, ref bool __result)
        {
            if (thought != ThoughtDefOf.SoakingWet)
            {
                return true; // terrain thoughts and everything else: vanilla logic untouched
            }
            Pawn pawn = PawnRef(__instance);
            if (!Gene_SlimeGel.IsSlime(pawn))
            {
                return true; // not a slime: vanilla logic untouched
            }
            pawn?.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(ThoughtDefOf.SoakingWet);
            __result = false;
            return false; // slimes never gain soaking wet
        }
    }

    /// <summary>
    /// Slimes are wild men: vanilla hardcodes <see cref="WildManUtility.IsWildMan"/> to
    /// the WildMan pawn kind, which drives taming (WorkGiver_Tame), arrest, wander-off
    /// behaviour and the wild-man labels. This postfix treats every slime pawn kind the
    /// same way, so wild slimes can be tamed and convinced to join exactly like wild men.
    /// </summary>
    /// <summary>The slime pawn kinds, shared by the wild-man patches and the think-tree node.</summary>
    public static class SlimeKinds
    {
        public static bool IsSlimeKind(PawnKindDef kind)
        {
            return kind?.defName == "PMM_Slime_Wild" || kind?.defName == "PMM_Slime_Bubble" ||
                   kind?.defName == "PMM_Slime_Taisui" || kind?.defName == "PMM_Slime_Sea";
        }
    }

    [HarmonyPatch(typeof(WildManUtility), nameof(WildManUtility.IsWildMan))]
    public static class Patch_SlimeIsWildMan
    {
        public static void Postfix(Pawn p, ref bool __result)
        {
            if (!__result && SlimeKinds.IsSlimeKind(p?.kindDef) && !p.IsSubhuman)
            {
                __result = true;
            }
        }
    }

    /// <summary>
    /// Stop wild slimes from marching to the map edge and despawning. Vanilla
    /// <see cref="WildManUtility.WildManShouldReachOutsideNow"/> returns true for any
    /// wild man who hasn't yet "reached outside", which makes the pawn walk to the
    /// nearest edge (CanOpenAnyDoor even opens doors). Vanilla wild men only linger
    /// because they spawn somewhere visible and the player notices and arrests/tames
    /// them before the walk finishes; a slime that wanders into a far, unexplored
    /// polluted cave is never spotted, so it just walks off and despawns. Report that
    /// wild slimes have already reached outside so the edge-walk never triggers and
    /// they wander the map until tamed.
    /// </summary>
    [HarmonyPatch(typeof(WildManUtility), nameof(WildManUtility.WildManShouldReachOutsideNow))]
    public static class Patch_SlimeShouldNotReachOutside
    {
        public static void Postfix(Pawn p, ref bool __result)
        {
            if (__result && SlimeKinds.IsSlimeKind(p?.kindDef))
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// Every slime is generated with the same life story. A postfix on
    /// <see cref="PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo"/> forces both
    /// backstory slots to the single slime childhood/adulthood after generation, so even
    /// callers that bypass the pawn kind's backstory filter cannot produce a different
    /// story.
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo))]
    public static class Patch_SlimeBackstory
    {
        private static readonly AccessTools.FieldRef<Pawn_StoryTracker, BackstoryDef> ChildhoodRef =
            AccessTools.FieldRefAccess<Pawn_StoryTracker, BackstoryDef>("childhood");
        private static readonly AccessTools.FieldRef<Pawn_StoryTracker, BackstoryDef> AdulthoodRef =
            AccessTools.FieldRefAccess<Pawn_StoryTracker, BackstoryDef>("adulthood");

        public static void Postfix(Pawn pawn)
        {
            // Detect the slime by its pawn KIND, not the slime-gel gene: this postfix runs
            // during generation (GiveAppropriateBioAndNameTo), which is BEFORE the
            // generator applies genes (GenerateGenes). At this point the slime-gel gene is
            // not yet active, so Gene_SlimeGel.IsSlime would return false and the forcing
            // would be skipped - leaving a random human backstory. The pawn kind is set
            // before the bio, so it is the reliable signal here.
            if (pawn?.story == null || !SlimeKinds.IsSlimeKind(pawn.kindDef))
            {
                return;
            }

            BackstoryDef childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail("PMM_SlimeChild");
            // Sea slimes get their own adulthood (with the Animals skill gain that makes
            // them natural fishers); every other slime gets the generic wandering adulthood.
            // Read the xenotype from the pawn KIND's set (the pawn's own genes aren't
            // applied yet, so genes.Xenotype is still null here).
            string adulthoodName = pawn.kindDef?.defName == "PMM_Slime_Sea"
                ? "PMM_SlimeSeaAdult"
                : "PMM_SlimeAdult";
            BackstoryDef adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail(adulthoodName);
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
}
