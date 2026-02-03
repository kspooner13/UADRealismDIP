using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Il2Cpp;
using UIOverall.Core;

#pragma warning disable CS8604
#pragma warning disable CS8625
#pragma warning disable CS8603

namespace UIOverall
{
    /// <summary>
    /// Baseline Harmony patches for the game's Ui class. These hooks run at key moments so UIOverhaulBase can overwrite or restyle UI.
    /// Add more [HarmonyPatch] methods here for additional screens or methods you want to hook.
    /// </summary>
    [HarmonyPatch(typeof(Ui))]
    internal static class Patch_Ui_Baseline
    {
        [HarmonyPatch(nameof(Ui.Start))]
        [HarmonyPostfix]
        internal static void Postfix_Start(Ui __instance)
        {
            UIOverhaulBase.OnUIAvailable(__instance);
        }

        [HarmonyPatch(nameof(Ui.ConstructorUI))]
        [HarmonyPostfix]
        internal static void Postfix_ConstructorUI(Ui __instance)
        {
            if (GameManager.IsConstructor)
                UIOverhaulBase.OnConstructorUIShown();
        }

        // Hook more Ui methods as needed, for example:
        // - ShowMainMenu / HideMainMenu → OnMainMenuShown
        // - Campaign-related Show* / Open* → OnCampaignUIShown
        // - Battle UI Show* → OnBattleUIShown
        // Use dnSpy/ILSpy on Assembly-CSharp to find exact method names for the screens you want to replace.
    }
}
