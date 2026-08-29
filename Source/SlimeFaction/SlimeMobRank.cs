using System.Reflection;
using HarmonyLib;
using IsekaiLeveling.MobRanking;
using Verse;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// Slimes are weak, low-rank monsters: in the fiction they sit at the bottom of the
    /// Mamono food chain, so none should ever SPAWN above rank E (level 10). The Isekai
    /// RPG Leveling mod injects a <see cref="MobRankComponent"/> into every factionless
    /// creature (slimes spawn factionless, like wild men) and rolls a rank on first
    /// access; ranks D and above map to levels well past 10. This postfix caps the spawn
    /// roll for slimes at E or F. It deliberately does NOT use SetRankOverride (which
    /// would pin the rank permanently) — it only lowers the freshly-rolled rank/level,
    /// so the slime can still gain XP and rank up past E through play afterwards.
    /// </summary>
    [HarmonyPatch(typeof(MobRankComponent), nameof(MobRankComponent.RecalculateRank))]
    public static class Patch_SlimeMobRankCap
    {
        /// <summary>Highest level a wild slime may spawn at (rank E's ceiling).</summary>
        private const int MaxSlimeLevel = 10;

        // Private state we adjust to lower the spawn roll without pinning the rank:
        //  cachedRank / cachedBaseRank — the displayed tier, clamped to at most E so it
        //      matches the capped level.
        //  cachedIsElite — cleared, since elite bonus levels could push past the cap.
        //  statsInitialized — reset so EnsureStatsInitialized re-derives stats + level
        //      from the clamped rank string.
        private static readonly FieldInfo CachedRankField =
            AccessTools.Field(typeof(MobRankComponent), "cachedRank");
        private static readonly FieldInfo CachedBaseRankField =
            AccessTools.Field(typeof(MobRankComponent), "cachedBaseRank");
        private static readonly FieldInfo CachedIsEliteField =
            AccessTools.Field(typeof(MobRankComponent), "cachedIsElite");
        private static readonly FieldInfo StatsInitializedField =
            AccessTools.Field(typeof(MobRankComponent), "statsInitialized");
        private static readonly MethodInfo EnsureStatsInitializedMethod =
            AccessTools.Method(typeof(MobRankComponent), "EnsureStatsInitialized");

        public static void Postfix(MobRankComponent __instance)
        {
            Pawn pawn = __instance.Pawn;
            if (!IsSlime(pawn) || __instance.currentLevel <= MaxSlimeLevel)
            {
                return; // not a slime, or already E/F — leave the roll alone
            }

            // Cap the spawn roll at the bottom of the ladder. F (levels 1-5) is the
            // common case; E (levels 6-10) is the uncommon "strong" slime. We lower the
            // freshly-rolled rank rather than pinning it: hasRankOverride stays false,
            // so this slime is free to level up and rank up past E later.
            MobRankTier clamped = Rand.Chance(0.25f) ? MobRankTier.E : MobRankTier.F;
            CachedRankField?.SetValue(__instance, clamped);
            CachedBaseRankField?.SetValue(__instance, clamped);
            CachedIsEliteField?.SetValue(__instance, false);

            // Re-derive stats and level from the clamped rank so everything stays
            // consistent (and the level lands at or below E's ceiling of 10).
            if (StatsInitializedField != null && EnsureStatsInitializedMethod != null)
            {
                StatsInitializedField.SetValue(__instance, false);
                EnsureStatsInitializedMethod.Invoke(__instance, new object[] { null });
            }

            // Belt and braces: even if stat generation overshoots, the spawn level
            // itself never exceeds the cap. currentLevel is a public field.
            if (__instance.currentLevel > MaxSlimeLevel)
            {
                __instance.currentLevel = MaxSlimeLevel;
            }
        }

        /// <summary>
        /// True for any slime pawn. Checks the slime pawn kind (set before genes during
        /// generation, so it catches generation-time rank rolls) and falls back to the
        /// slime-gel gene (catches pawns that became slimes later, e.g. jelly ingestion
        /// or a completed parasite takeover, whose rank is re-rolled on transformation).
        /// </summary>
        private static bool IsSlime(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }
            return SlimeKinds.IsSlimeKind(pawn.kindDef) || Gene_SlimeGel.IsSlime(pawn);
        }
    }
}
