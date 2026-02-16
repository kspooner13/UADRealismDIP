using HarmonyLib;
using Il2Cpp;

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
            // Only run when in campaign; otherwise campaign top panel may not exist and can freeze.
            if (!GameManager.IsCampaign || CampaignController.Instance == null)
                return;
            IntelUI.Initialize();
        }
    }
}
