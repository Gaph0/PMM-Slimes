using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// Entry point. Applies Harmony patches when the mod loads.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SlimeMod
    {
        static SlimeMod()
        {
            var harmony = new Harmony("PMM.SlimeFaction");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    /// <summary>
    /// Finalises the slime momo race defs. Runs after def loading, so the def database
    /// is safe to touch. leatherDef must go: an empty &lt;leatherDef /&gt; in XML would
    /// log a cross-reference error at startup, while a C# null is silent — and it hides
    /// the "leather type" info row and every vanilla leather yield. (meatDef needs no
    /// C# help: specificMeatDef in XML handles it.)
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SlimeRaceSetup
    {
        static SlimeRaceSetup()
        {
            NullLeather(SlimeDefOf.PMM_Race_SlimeMomo);
            NullLeather(SlimeDefOf.PMM_Race_SlimeMomoRed);
            NullLeather(SlimeDefOf.PMM_Race_SlimeMomoBubble);
            NullLeather(SlimeDefOf.PMM_Race_SlimeMomoDark);
            NullLeather(SlimeDefOf.PMM_Race_SlimeMomoTaisui);
            NullLeather(SlimeDefOf.PMM_Race_SlimeMomoSea);
        }

        private static void NullLeather(ThingDef race)
        {
            if (race?.race != null)
            {
                race.race.leatherDef = null;
            }
        }
    }

    [DefOf]
    public static class SlimeDefOf
    {
        public static GeneDef PMM_Gene_SlimeGel;
        public static ThingDef PMM_SlimeJelly;
        public static ThingDef PMM_SlimeJellyBlue;
        public static ThingDef PMM_SlimeJellyBubble;
        public static ThingDef PMM_SlimeJellyRed;
        public static ThingDef PMM_SlimeJellyDark;
        public static ThingDef PMM_SlimeJellyTaisui;
        public static ThingDef PMM_SlimeJellySea;
        public static ThingDef PMM_SlimeJellyMochi;
        public static HediffDef PMM_Hediff_SeaParalytic;
        public static HediffDef PMM_Hediff_SlimeJellyOozing;
        public static HediffDef PMM_Hediff_ParasiteCarrier;
        public static HediffDef PMM_Hediff_ParasiteTakeover;
        public static ThingDef PMM_ParasiteSlime;
        public static ThingDef PMM_Race_SlimeMomo;
        public static ThingDef PMM_Race_SlimeMomoRed;
        public static ThingDef PMM_Race_SlimeMomoBubble;
        public static ThingDef PMM_Race_SlimeMomoDark;
        public static ThingDef PMM_Race_SlimeMomoTaisui;
        public static ThingDef PMM_Race_SlimeMomoSea;

        static SlimeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SlimeDefOf));
        }
    }

    /// <summary>
    /// Maps a slime xenotype to its per-colour race and jelly. The race carries the
    /// matching specificMeatDef, so a slime's info page / butcher tooltip shows the
    /// jelly it actually drops. Shared by the generation patch and the butcher patch.
    /// </summary>
    public static class SlimeRaces
    {
        /// <summary>Race def for the given xenotype defName (defaults to blue).</summary>
        public static ThingDef RaceFor(string xenotypeDefName)
        {
            switch (xenotypeDefName)
            {
                case "PMM_Slime_Red": return SlimeDefOf.PMM_Race_SlimeMomoRed;
                case "PMM_Slime_Bubble": return SlimeDefOf.PMM_Race_SlimeMomoBubble;
                case "PMM_Slime_Dark": return SlimeDefOf.PMM_Race_SlimeMomoDark;
                case "PMM_Slime_Taisui": return SlimeDefOf.PMM_Race_SlimeMomoTaisui;
                case "PMM_Slime_Sea": return SlimeDefOf.PMM_Race_SlimeMomoSea;
                default: return SlimeDefOf.PMM_Race_SlimeMomo;
            }
        }

        /// <summary>Jelly def for the given xenotype defName. Each slime drops its own
        /// named jelly; anything without a recognised xenotype falls back to the generic
        /// <see cref="SlimeDefOf.PMM_SlimeJelly"/>.</summary>
        public static ThingDef JellyFor(string xenotypeDefName)
        {
            switch (xenotypeDefName)
            {
                case "PMM_Slime_Blue": return SlimeDefOf.PMM_SlimeJellyBlue;
                case "PMM_Slime_Red": return SlimeDefOf.PMM_SlimeJellyRed;
                case "PMM_Slime_Bubble": return SlimeDefOf.PMM_SlimeJellyBubble;
                case "PMM_Slime_Dark": return SlimeDefOf.PMM_SlimeJellyDark;
                case "PMM_Slime_Taisui": return SlimeDefOf.PMM_SlimeJellyTaisui;
                case "PMM_Slime_Sea": return SlimeDefOf.PMM_SlimeJellySea;
                case "PMM_Slime_Nureonago": return SlimeDefOf.PMM_SlimeJellyMochi;
                default: return SlimeDefOf.PMM_SlimeJelly;
            }
        }

        /// <summary>Xenotype defName that a given jelly monsterises a woman into
        /// (defaults to blue). The inverse of <see cref="JellyFor"/>: each slime's jelly
        /// melts a woman into that slime's own colour.</summary>
        public static string XenotypeFor(ThingDef jelly)
        {
            if (jelly == SlimeDefOf.PMM_SlimeJellyRed) return "PMM_Slime_Red";
            if (jelly == SlimeDefOf.PMM_SlimeJellyBubble) return "PMM_Slime_Bubble";
            if (jelly == SlimeDefOf.PMM_SlimeJellyDark) return "PMM_Slime_Dark";
            if (jelly == SlimeDefOf.PMM_SlimeJellyTaisui) return "PMM_Slime_Taisui";
            if (jelly == SlimeDefOf.PMM_SlimeJellySea) return "PMM_Slime_Sea";
            if (jelly == SlimeDefOf.PMM_SlimeJellyMochi) return "PMM_Slime_Nureonago";
            // Blue slime jelly, the generic slime jelly, and any unrecognised jelly all
            // monsterise a woman into a blue (default) slime.
            return "PMM_Slime_Blue";
        }
    }

    /// <summary>
    /// Point a generated slime at the race matching its rolled xenotype. The pawn kind
    /// always specifies the blue race, so without this a red/bubble/dark slime would
    /// carry the blue race def and its info page would show the wrong (blue) jelly.
    /// Runs after genes are applied, so the xenotype is already set. A no-op for
    /// non-slime pawns, so vanilla and other races are untouched.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn),
        new[] { typeof(PawnGenerationRequest) })]
    public static class Patch_SlimeRaceByXenotype
    {
        /// <summary>Private Pawn_GuestTracker.hostFactionInt, for de-guesting the nureonago.</summary>
        private static readonly AccessTools.FieldRef<Pawn_GuestTracker, Faction> HostFactionRef =
            AccessTools.FieldRefAccess<Pawn_GuestTracker, Faction>("hostFactionInt");

        public static void Postfix(Pawn __result)
        {
            // A dev-spawned (or otherwise non-incident) nureonago never passes through the
            // visit incident, so prepare her here too. PawnKindDef has no comps field, so
            // the visitor comp cannot be declared in XML; it is added here and by the incident.
            // She is also NOT a baseliner when she is dev spawned: dev spawning makes a
            // factionless pawn, and PawnGenerator throws away the rolled xenotype of a
            // factionless baseliner and re-rolls it from XenotypeDef.factionlessGenerationWeight
            // (so she can come out Yttakin or any other random xenotype). The visit incident
            // passes Faction.OfPlayer, so the real event still gets her as a baseliner.
            if (__result?.kindDef?.defName == "PMM_Slime_Nureonago")
            {
                // Attach the wait/leave brain (idempotent: the incident may have added one).
                if (__result.TryGetComp<NureonagoVisitorComp>() == null)
                {
                    NureonagoVisitorComp comp = new NureonagoVisitorComp();
                    comp.parent = __result;
                    __result.AllComps.Add(comp);
                }

                // A dev-spawn never runs the incident, so it never gets the guest-state clear.
                // PawnGenerator assigns her a host faction, and ThinkNode_ConditionalGuest
                // (vanilla Humanlike tree) makes any non-prisoner with a host faction walk off
                // the map via ExitMapBest. Clearing the host faction makes her a non-guest, so
                // she stays put. FieldRef: hostFactionInt is private on Pawn_GuestTracker.
                if (__result.guest != null)
                {
                    HostFactionRef(__result.guest) = null;
                }
            }

            // Same preparation for a slime carrier: attach the wait/leave brain, de-guest
            // her, and carrier-ify her in secret. This also runs for the visit incident
            // itself (generation routes through this postfix); everything is idempotent.
            if (__result?.kindDef?.defName == IncidentWorker_CarrierSlimeVisit.CarrierKind)
            {
                if (__result.TryGetComp<CarrierSlimeVisitorComp>() == null)
                {
                    CarrierSlimeVisitorComp comp = new CarrierSlimeVisitorComp();
                    comp.parent = __result;
                    __result.AllComps.Add(comp);
                }
                if (__result.guest != null)
                {
                    HostFactionRef(__result.guest) = null;
                }
                SlimeCarrierUtility.MakeCarrier(__result, revealed: false);
                // A dev-spawn never runs the incident, so mask her backstories here too
                // (the incident masks its own right after this postfix runs).
                CarrierSlimeBackstories.Mask(__result);
            }

            if (__result == null || !Gene_SlimeGel.IsSlime(__result))
            {
                return;
            }
            // A slime carrier keeps her Human race (human skin and meat when butchered);
            // belt-and-braces: MakeCarrier normally adds the genes post-generation, so
            // this postfix never even sees her gel gene.
            if (SlimeCarrierUtility.IsCarrier(__result))
            {
                return;
            }
            ThingDef race = SlimeRaces.RaceFor(__result.genes?.Xenotype?.defName);
            if (race != null && __result.def != race)
            {
                __result.def = race;
            }
        }
    }

    /// <summary>
    /// The slime gel gene. Adds (and removes) the hidden jelly-oozing hediff; the rest of
    /// the slime physiology is done by the Harmony patches, which key off
    /// <see cref="IsSlime"/>.
    /// </summary>
    public class Gene_SlimeGel : Gene
    {
        /// <summary>True if the pawn has an active slime gel gene.</summary>
        public static bool IsSlime(Pawn pawn)
        {
            if (pawn?.genes == null)
            {
                return false;
            }
            Gene gene = pawn.genes.GetGene(SlimeDefOf.PMM_Gene_SlimeGel);
            return gene != null && gene.Active;
        }

        /// <summary>Start oozing slime jelly when the gene is added.</summary>
        public override void PostAdd()
        {
            base.PostAdd();
            // Slime carriers carry the gel gene but never ooze: the parasite keeps every
            // drop for its host. MakeCarrier adds the carrier marker BEFORE the genes, so
            // this guard is already true when the gel gene lands on a carrier-to-be.
            if (pawn?.health != null && !pawn.Dead &&
                !SlimeCarrierUtility.IsCarrier(pawn) &&
                pawn.health.hediffSet.GetFirstHediffOfDef(SlimeDefOf.PMM_Hediff_SlimeJellyOozing) == null)
            {
                pawn.health.AddHediff(SlimeDefOf.PMM_Hediff_SlimeJellyOozing);
            }
        }

        /// <summary>Stop oozing slime jelly when the gene is removed.</summary>
        public override void PostRemove()
        {
            base.PostRemove();
            Hediff oozing = pawn?.health?.hediffSet?.GetFirstHediffOfDef(SlimeDefOf.PMM_Hediff_SlimeJellyOozing);
            if (oozing != null)
            {
                pawn.health.RemoveHediff(oozing);
            }
        }
    }

    /// <summary>
    /// Restores the slime's translucent alpha. The slime skin genes declare a 0.75 alpha
    /// (the 4th component of skinColorOverride), but vanilla applies gene skin colours
    /// through GenColor.ClampToValueRange, which converts to HSV and back via
    /// Color.HSVToRGB - and HSVToRGB always returns alpha 1, silently destroying the
    /// declared transparency. This postfix re-applies the slime alpha to every SkinColor
    /// read for a true slime (not a carrier), so the gel body and (via Patch_SlimeHairColor)
    /// the hair render semi-transparent. RGB is left untouched.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_StoryTracker), nameof(Pawn_StoryTracker.SkinColor), MethodType.Getter)]
    public static class Patch_SlimeSkinAlpha
    {
        /// <summary>Slime gel opacity: 0.75 transparency = 75% opaque.</summary>
        private const float SlimeAlpha = 0.75f;

        public static void Postfix(Pawn_StoryTracker __instance, Pawn ___pawn, ref Color __result)
        {
            if (Gene_SlimeGel.IsSlime(___pawn) && !SlimeCarrierUtility.IsCarrier(___pawn))
            {
                __result.a = SlimeAlpha;
            }
        }
    }

    /// <summary>
    /// Makes slime hair render in the pawn's skin colour, so a slime's hair always
    /// matches its gel body (blue slime = blue hair, red slime = red hair, ...).
    /// A postfix on the render node's colour lookup means it holds no matter how the
    /// pawn was spawned or restyled, and slimes never show grey hair from ageing.
    /// Patches the base PawnRenderNode.ColorFor (dispatched per-node) and only acts
    /// when the node is the hair node of a slime-gel pawn.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNode), nameof(PawnRenderNode.ColorFor))]
    public static class Patch_SlimeHairColor
    {
        public static void Postfix(PawnRenderNode __instance, Pawn pawn, ref Color __result)
        {
            // Carriers keep their natural hair colour: only the gel body of a true slime
            // gets the hair=skin gel look.
            if (__instance is PawnRenderNode_Hair && pawn?.story != null &&
                Gene_SlimeGel.IsSlime(pawn) && !SlimeCarrierUtility.IsCarrier(pawn))
            {
                __result = pawn.story.SkinColor;
            }
        }
    }

    /// <summary>
    /// Slimes never have tattoos: their bodies are semi-liquid gel, so ink would not hold.
    /// The setter postfix forces every tattoo assignment on a slime to NoTattoo_*
    /// (covers pawn generation, styling stations, dev tools and save-loaded pawns when
    /// restyled), while the getter postfix hides any tattoo already stored on existing
    /// pawns in older saves.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_StyleTracker), nameof(Pawn_StyleTracker.FaceTattoo), MethodType.Setter)]
    public static class Patch_SlimeFaceTattooSetter
    {
        // faceTattoo is private; write it via Traverse to bypass the setter's storage.
        private static readonly AccessTools.FieldRef<Pawn_StyleTracker, TattooDef> FaceTattooRef =
            AccessTools.FieldRefAccess<Pawn_StyleTracker, TattooDef>("faceTattoo");

        public static void Postfix(Pawn_StyleTracker __instance, Pawn ___pawn)
        {
            if (Gene_SlimeGel.IsSlime(___pawn))
            {
                FaceTattooRef(__instance) = TattooDefOf.NoTattoo_Face;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_StyleTracker), nameof(Pawn_StyleTracker.BodyTattoo), MethodType.Setter)]
    public static class Patch_SlimeBodyTattooSetter
    {
        // bodyTattoo is private; write it via Traverse to bypass the setter's storage.
        private static readonly AccessTools.FieldRef<Pawn_StyleTracker, TattooDef> BodyTattooRef =
            AccessTools.FieldRefAccess<Pawn_StyleTracker, TattooDef>("bodyTattoo");

        public static void Postfix(Pawn_StyleTracker __instance, Pawn ___pawn)
        {
            if (Gene_SlimeGel.IsSlime(___pawn))
            {
                BodyTattooRef(__instance) = TattooDefOf.NoTattoo_Body;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_StyleTracker), nameof(Pawn_StyleTracker.FaceTattoo), MethodType.Getter)]
    public static class Patch_SlimeFaceTattooGetter
    {
        public static void Postfix(Pawn_StyleTracker __instance, Pawn ___pawn, ref TattooDef __result)
        {
            if (__result != null && __result != TattooDefOf.NoTattoo_Face && Gene_SlimeGel.IsSlime(___pawn))
            {
                __result = TattooDefOf.NoTattoo_Face;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_StyleTracker), nameof(Pawn_StyleTracker.BodyTattoo), MethodType.Getter)]
    public static class Patch_SlimeBodyTattooGetter
    {
        public static void Postfix(Pawn_StyleTracker __instance, Pawn ___pawn, ref TattooDef __result)
        {
            if (__result != null && __result != TattooDefOf.NoTattoo_Body && Gene_SlimeGel.IsSlime(___pawn))
            {
                __result = TattooDefOf.NoTattoo_Body;
            }
        }
    }

}
