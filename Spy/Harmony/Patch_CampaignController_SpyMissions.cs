using HarmonyLib;
using Il2Cpp;

namespace Spy.Harmony
{
    /// <summary>
    /// After each turn (OnNewTurn), progress all spies "On Mission": decrement turns, roll success/fail when duration ends.
    /// </summary>
    [HarmonyPatch(typeof(CampaignController), nameof(CampaignController.OnNewTurn))]
    internal static class Patch_CampaignController_SpyMissions
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            SpyMissionProgress.ProgressMissionsThisTurn();
        }
    }
}
