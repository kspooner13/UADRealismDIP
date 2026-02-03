using System;
using UnityEngine;
using Il2Cpp;
using UIOverall.Utils;

#pragma warning disable CS8601
#pragma warning disable CS8604
#pragma warning disable CS8625
#pragma warning disable CS8603

namespace UIOverall.Core
{
    /// <summary>
    /// Central hook point for UI overhaul logic. Goal: replace IMGUI with Unity UI Toolkit (UIElements).
    /// Harmony patches call these methods at key UI lifecycle moments; extend them to show UI Toolkit
    /// panels instead of (or in place of) existing OnGUI / IMGUI code.
    /// </summary>
    public static class UIOverhaulBase
    {
        /// <summary>Game Ui instance (G.ui). Set after Ui.Start runs.</summary>
        public static Ui? UiInstance { get; private set; }

        /// <summary>Root GameObject of the main UI. Use for FindDeepChild and hierarchy traversal; parent UIDocument/UIToolkit here.</summary>
        public static GameObject? UiRoot => UiInstance?.gameObject;

        /// <summary>Whether the UI has been initialized (Start has run).</summary>
        public static bool IsUIAvailable => UiInstance != null;

        /// <summary>Try to find Ui in the current scene and set UiInstance. Call when main menu scene has loaded but G.ui may not be set yet.</summary>
        public static bool TrySetUiFromScene()
        {
            if (UiInstance != null)
                return true;
            Ui? ui = UnityEngine.Object.FindObjectOfType<Ui>();
            if (ui != null)
            {
                UiInstance = ui;
                return true;
            }
            return false;
        }

        /// <summary>Called once when the game's Ui component has started. Cache references; create PanelSettings / UIToolkit setup for replacing IMGUI.</summary>
        public static void OnUIAvailable(Ui ui)
        {
            UiInstance = ui;
#if USE_UI_TOOLKIT
            if (GameManager.IsMainMenu)
                UIOverall.UIToolkit.MainMenuReplacement.Show();
#endif
        }

        /// <summary>Called when the main menu / overlay UI is shown. Replace IMGUI main menu with UI Toolkit panel here.</summary>
        public static void OnMainMenuShown()
        {
            // Extend: show UIToolkit panel (buttons, panels) instead of OnGUI main menu
        }

        /// <summary>Called when campaign UI is shown. Replace IMGUI campaign screens with UI Toolkit panels here.</summary>
        public static void OnCampaignUIShown()
        {
            // Extend: show UIToolkit panels for campaign map, side panels, dialogs
        }

        /// <summary>Called when the ship constructor UI is shown. Replace IMGUI constructor with UI Toolkit panels here.</summary>
        public static void OnConstructorUIShown()
        {
            // Extend: show UIToolkit panels for hull sliders, part lists, stats
        }

        /// <summary>Called when battle UI is shown. Replace IMGUI battle HUD with UI Toolkit panels here.</summary>
        public static void OnBattleUIShown()
        {
            // Extend: show UIToolkit panels for speed controls, ship panels, minimap
        }

        /// <summary>Helper: find a descendant by name under the UI root. Returns null if not found.</summary>
        public static GameObject FindInUI(string childName, bool allowInactive = true)
        {
            return UiRoot != null ? UIUtils.FindDeepChild(UiRoot, childName, allowInactive) : null;
        }
    }
}
