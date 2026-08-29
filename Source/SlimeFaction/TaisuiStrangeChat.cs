using HarmonyLib;
using RimWorld;
using Verse;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// Taisui pawns are "disturbing" conversationalists, like Anomaly's Void Touched:
    /// vanilla <see cref="Pawn_InteractionsTracker.TryInteractRandomly"/> reads
    /// <see cref="Pawn_StoryTracker.IsDisturbing"/> to decide whether an interaction
    /// becomes the special <c>DisturbingChat</c> ("strange chat") instead of a normal
    /// chitchat/deep talk. This postfix makes a taisui report as disturbing a small
    /// fraction of the time, so about one in ten of their social interactions come out
    /// as a strange chat — the incomprehensible, too-advanced speech the lore describes.
    ///
    /// Soft Anomaly compat: the property already returns false whenever Anomaly is
    /// inactive, and <c>DisturbingChat</c> is an Anomaly def, so without Anomaly this
    /// patch is a no-op (the result stays false and vanilla picks a normal interaction).
    ///
    /// Chance is rolled per read, so pawns that are already disturbing (Void Touched
    /// hediff or the Disturbing trait) are unaffected — the result stays true.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_StoryTracker), nameof(Pawn_StoryTracker.IsDisturbing), MethodType.Getter)]
    public static class Patch_TaisuiIsDisturbing
    {
        /// <summary>
        /// Chance a taisui reads as disturbing on any given social-interaction pick.
        /// 0.10 = roughly one in ten interactions become a strange chat.
        /// </summary>
        private const float StrangeChatChance = 0.10f;

        public static void Postfix(Pawn_StoryTracker __instance, Pawn ___pawn, ref bool __result)
        {
            // Only widen false -> true for a taisui; never disturb a pawn that is
            // already disturbing (leave __result true), and never act without Anomaly.
            if (!__result && ModsConfig.AnomalyActive &&
                ___pawn?.genes?.Xenotype?.defName == "PMM_SlimeTaisui" &&
                Rand.Value < StrangeChatChance)
            {
                __result = true;
            }
        }
    }
}
