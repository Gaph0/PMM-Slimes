using System;
using Verse;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// Soft-detection of Vanilla Psycasts Expanded. The SlimeCore psycast path
    /// and its ability defs are plain XML referencing VPE/VEF types, which is
    /// fine — RimWorld resolves abilityClass lazily and def-loading tolerates the
    /// missing modExtension classes when VPE is absent (the path itself is only
    /// ever granted by Project Momo's VPE integration, which is inert then too).
    /// Everything in SlimePsycasts.cs touches VEF/VPE types, so the psycast
    /// classes are only ever instantiated while VPE is loaded; this gate keeps
    /// the rest of the mod honest.
    /// </summary>
    public static class SlimeVPECompat
    {
        public const string VpePackageId = "VanillaExpanded.VPsycastsE";

        public static bool Active
        {
            get
            {
                foreach (var mod in LoadedModManager.RunningMods)
                {
                    if (string.Equals(mod.PackageIdPlayerFacing, VpePackageId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(mod.PackageId, VpePackageId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        static SlimeVPECompat()
        {
            Log.Message($"[PMM Slime Faction] Vanilla Psycasts Expanded active = {Active}." +
                (Active ? "" : " SlimeCore psycast path will load inert."));
        }
    }
}
