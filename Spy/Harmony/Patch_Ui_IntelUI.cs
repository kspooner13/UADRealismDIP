using HarmonyLib;
using Il2Cpp;

namespace Spy.Harmony
{
    /// <summary>
    /// When in campaign, ensure Intel UI is initialized (button + window).
    /// ApplyCampaignWindowModifications runs only at startup (before campaign), so we init from Update once we're in campaign.
    /// </summary>
    [HarmonyPatch(typeof(Ui), nameof(Ui.Update))]
    internal static class Patch_Ui_IntelUI
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            if (IntelUI.IsInitialized)
                return;
            if (!GameManager.IsCampaign || CampaignController.Instance == null)
                return;
            IntelUI.Initialize();
        }
    }
}
