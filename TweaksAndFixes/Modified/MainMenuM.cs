using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using UnityEngine.UI;
using Il2CppTMPro;
using TweaksAndFixes.Data;
using System.Reflection;

#pragma warning disable CS8600
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8618

namespace TweaksAndFixes
{
    /// <summary>
    /// Main menu modifications: adds buttons for Mod Updates and TAF Updates.
    /// </summary>
    public static class MainMenu
    {
        private const string MenuButtonsPath = "Global/Ui/UiMain/MainMenu/Layout/MenuButtons";
        private const string ContinueButtonPath = "Global/Ui/UiMain/MainMenu/Layout/MenuButtons/Continue";
        private const string NewsButtonPath = "Global/Ui/UiMain/MainMenu/Layout/MenuButtons/News";

        private static bool _initialized;

        public static void Start()
        {
            if (_initialized)
                return;
            _initialized = true;
            SetupMainMenuButtons();
        }

        private static void SetupMainMenuButtons()
        {
            GameObject menuButtons = ModUtils.GetChildAtPath(MenuButtonsPath);
            GameObject continueButton = ModUtils.GetChildAtPath(ContinueButtonPath);
            GameObject newsButton = ModUtils.GetChildAtPath(NewsButtonPath);
            if (menuButtons == null || continueButton == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("MainMenu: Could not find MenuButtons or Continue - skipping main menu button setup.");
                return;
            }
            if (newsButton == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("MainMenu: Could not find News button - placing new buttons at end of menu.");
            }

            int insertIndex = newsButton != null ? newsButton.transform.GetSiblingIndex() + 1 : menuButtons.transform.childCount;
            MakeAndConfigButton(menuButtons, "Mod Updates", "$TAF_Ui_MainMenu_ModUpdates", "$TAF_Ui_MainMenu_ModUpdates_Tooltip", OnModUpdatesClick, insertIndex);
            MakeAndConfigButton(menuButtons, "TAF Updates", "$TAF_Ui_MainMenu_TAFUpdates", "$TAF_Ui_MainMenu_TAFUpdates_Tooltip", OnTAFUpdatesClick, insertIndex + 1);
            //AddMainMenuButton(menuButtons, continueButton, "TAF_ModUpdates", "$TAF_Ui_MainMenu_ModUpdates", "$TAF_Ui_MainMenu_ModUpdates_Tooltip", OnModUpdatesClick, insertIndex);
            //AddMainMenuButton(menuButtons, continueButton, "TAF_TAFUpdates", "$TAF_Ui_MainMenu_TAFUpdates", "$TAF_Ui_MainMenu_TAFUpdates_Tooltip", OnTAFUpdatesClick, insertIndex + 1);
        }

        private static void AddMainMenuButton(
            GameObject menuButtons,
            GameObject templateButton,
            string buttonName,
            string locKey,
            string tooltipKey,
            System.Action onClick,
            int siblingIndex)
        {
            GameObject clone = GameObject.Instantiate(templateButton);
            clone.transform.SetParent(menuButtons.transform);
            clone.name = buttonName;
            clone.SetActive(true);
            clone.transform.localScale = Vector3.one;

            clone.TryDestroyComponent<OnEnter>();
            clone.TryDestroyComponent<OnLeave>();
            clone.TryDestroyComponent<LocalizeText>();
            UiM.AddTooltip(clone, tooltipKey);

            GameObject textObj = clone.GetChild("Text");
            GameObject actualText = textObj.GetChild("Text (TMP)");
            MelonLoader.MelonLogger.Msg("Text: " + textObj.name);
            MelonLoader.MelonLogger.Msg("Actual Text: " + actualText.name);
            if (textObj != null && textObj.TryGetComponent(out TextMeshProUGUI tmpText))
                UiM.CreateLocalizedTextTag(clone, tmpText, locKey);

            UiM.SetButtonOnClick(clone, onClick);
            clone.transform.SetSiblingIndex(siblingIndex);
        }
        /// <summary>
        /// Makes and configures a button for a popup window.  Taken from the CheatMenu file, since it works pretty well.  Should move to ModUtil or UiM.
        /// </summary>
        /// <param name="window">The window to add the button to.</param>
        /// <param name="label">The label of the button.</param>
        /// <param name="tag">The tag of the button.</param>
        /// <param name="tooltip">The tooltip of the button.</param>
        /// <param name="onPress">The action to perform when the button is pressed.</param>
        private static void MakeAndConfigButton(GameObject window, string label, string tag, string tooltip, System.Action onPress=null, int siblingIndex=0)
        {
            string displayText = LocalizeManager.Localize(tag);
            GameObject spacerTemplate = ModUtils.GetChildAtPath("Global/Ui/UiMain/Popup/PopupMenu/Window/Spacer");
            GameObject buttonTemplate = ModUtils.GetChildAtPath("Global/Ui/UiMain/MainMenu/Layout/MenuButtons/Continue");
            if (buttonTemplate == null) return;

            GameObject btn = GameObject.Instantiate(buttonTemplate);
            btn.transform.SetParent(window, false);
            btn.name = "TAF_MainMenu_" + label.Replace(" ", "_");
            btn.SetActive(true);
            btn.transform.localPosition = Vector3.zero;
            btn.transform.localScale = Vector3.one;

            UiM.SetLocalizedTextTag(btn.GetChild("Text (TMP)"), tag);
            UiM.AddTooltip(btn, tooltip);
            Button b = btn.GetComponent<Button>();
            if (onPress != null)
            {
                b.onClick.AddListener(new System.Action(onPress));
            }
            if (siblingIndex >= 0) {
                btn.transform.SetSiblingIndex(siblingIndex);
            }
        }
        private static void OnModUpdatesClick()
        {
            // TODO: Open Mod Updates UI / flow
            Melon<TweaksAndFixes>.Logger.Msg("Mod Updates clicked.");
        }

        private static void OnTAFUpdatesClick()
        {
            // TODO: Open TAF Updates UI / flow
            Melon<TweaksAndFixes>.Logger.Msg("TAF Updates clicked.");
        }
    }
}
