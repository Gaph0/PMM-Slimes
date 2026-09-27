using ProjectMamono;
using Verse;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// Puts the six slime mamono corpses under the family's shared "mamono corpses" line.
    ///
    /// The category def and all of the moving live in the core mod now
    /// (ProjectMamono.MamonoCorpses, Defs/ThingCategoryDefs/ThingCategories_MamonoCorpses.xml), so
    /// the insects, the slimes and the elementals share one line instead of one line each.
    /// See MamonoCorpses for why a corpse's category cannot be set in XML.
    ///
    /// The races come from SlimeDefOf rather than from strings, so renaming a race def
    /// fails the build instead of quietly dropping that race off the line.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SlimeMamonoCorpses
    {
        static SlimeMamonoCorpses()
        {
            MamonoCorpses.Register(
                SlimeDefOf.PMM_Race_SlimeMamono.defName,
                SlimeDefOf.PMM_Race_SlimeMamonoRed.defName,
                SlimeDefOf.PMM_Race_SlimeMamonoBubble.defName,
                SlimeDefOf.PMM_Race_SlimeMamonoDark.defName,
                SlimeDefOf.PMM_Race_SlimeMamonoTaisui.defName,
                SlimeDefOf.PMM_Race_SlimeMamonoSea.defName);
        }
    }
}
