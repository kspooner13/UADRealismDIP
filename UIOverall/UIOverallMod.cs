using HarmonyLib;
using MelonLoader;

[assembly: MelonGame("Game Labs", "Ultimate Admiral Dreadnoughts")]
[assembly: MelonInfo(typeof(UIOverall.UIOverallMod), "UIOverall", "0.1.0", "UAD_TAF")]

namespace UIOverall
{
    /// <summary>
    /// UIOverall mod: baseline DLL for overhauling the game's UI. Goal: replace IMGUI with Unity UI Toolkit (UIElements).
    /// Applies Harmony patches to hook UI lifecycle and key screens; extend UIOverhaulBase to show UI Toolkit panels instead of OnGUI.
    /// </summary>
    public class UIOverallMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            HarmonyInstance.PatchAll(MelonAssembly.Assembly);
            base.OnInitializeMelon();
            Melon<UIOverallMod>.Logger.Msg("UIOverall loaded — UI overhaul baseline active.");
        }

        public override void OnDeinitializeMelon()
        {
            base.OnDeinitializeMelon();
        }

        /// <summary>Main menu is a scene (MainMenu or LevelMainMenu), not a GameObject. Show our UI Toolkit replacement when that scene loads.</summary>
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "MainMenu" || sceneName == "LevelMainMenu")
            {
                Melon<UIOverallMod>.Logger.Msg($"[UIOverall] Main menu scene loaded: {sceneName}");
#if USE_UI_TOOLKIT
                MelonCoroutines.Start(ShowMainMenuAfterSceneReady());
#endif
            }
        }

        /// <summary>When leaving main menu scene, hide our replacement so it can show again if we return.</summary>
        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (sceneName == "MainMenu" || sceneName == "LevelMainMenu")
            {
#if USE_UI_TOOLKIT
                UIOverall.UIToolkit.MainMenuReplacement.Hide();
#endif
            }
        }

#if USE_UI_TOOLKIT
        private static System.Collections.IEnumerator ShowMainMenuAfterSceneReady()
        {
            yield return null;
            yield return null;
            UIOverall.UIToolkit.MainMenuReplacement.Show();
        }
#endif
    }
}
