using ProjectMomo;
using Verse;

namespace PMM_SlimeFaction
{
    /// <summary>
    /// Puts the six slime momo corpses under the family's shared "momo corpses" line.
    ///
    /// The category def and all of the moving live in the core mod now
    /// (ProjectMomo.MomoCorpses, Defs/ThingCategoryDefs/ThingCategories_MomoCorpses.xml), so
    /// the insects, the slimes and the elementals share one line instead of one line each.
    /// See MomoCorpses for why a corpse's category cannot be set in XML.
    ///
    /// The races come from SlimeDefOf rather than from strings, so renaming a race def
    /// fails the build instead of quietly dropping that race off the line.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SlimeMomoCorpses
    {
        static SlimeMomoCorpses()
        {
            MomoCorpses.Register(
                SlimeDefOf.PMM_Race_SlimeMomo.defName,
                SlimeDefOf.PMM_Race_SlimeMomoRed.defName,
                SlimeDefOf.PMM_Race_SlimeMomoBubble.defName,
                SlimeDefOf.PMM_Race_SlimeMomoDark.defName,
                SlimeDefOf.PMM_Race_SlimeMomoTaisui.defName,
                SlimeDefOf.PMM_Race_SlimeMomoSea.defName);
        }
    }
}
