using HarmonyLib;

namespace Spy.Harmony
{
    /// <summary>
    /// Runs after TAF's ApplyCampaignWindowModifications so the campaign top panel exists.
    /// We then add the Intel button and wire our window.
    /// </summary>
    [HarmonyPatch(typeof(TweaksAndFixes.UiM), nameof(TweaksAndFixes.UiM.ApplyCampaignWindowModifications))]
    internal static class Patch_UiM_IntelUI
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            IntelUI.Initialize();
        }
    }
}
