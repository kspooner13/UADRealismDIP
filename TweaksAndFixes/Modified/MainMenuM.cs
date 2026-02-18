using System.IO;
using System.Text;
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
            GameObject optionsButton = ModUtils.GetChildAtPath("Global/Ui/UiMain/Common/Options/Options");
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
            newsButton.SetActive(false);
            optionsButton.SetActive(false);
            //AddMainMenuButton(menuButtons, continueButton, "TAF_ModUpdates", "$TAF_Ui_MainMenu_ModUpdates", "$TAF_Ui_MainMenu_ModUpdates_Tooltip", OnModUpdatesClick, insertIndex);
            //AddMainMenuButton(menuButtons, continueButton, "TAF_TAFUpdates", "$TAF_Ui_MainMenu_TAFUpdates", "$TAF_Ui_MainMenu_TAFUpdates_Tooltip", OnTAFUpdatesClick, insertIndex + 1);
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

            UiM.SetLocalizedTextTag(btn.GetChild("Text"), tag);
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
        private static GameObject _modUpdatesPopup;
        private static TMP_Text _modUpdatesBodyText;
        // Expected CSV: first row = header, then one row per update. Lines starting with # are skipped.
        // Example: Date,Mod,Version,Notes
        //          2025-02-01,Tweaks and Fixes,1.0.0,Added Mod Updates panel. Use "quotes" for commas in a cell.
        private const string ModUpdatesCsvName = "modupdates.csv";

        private static void OnModUpdatesClick()
        {
            EnsureModUpdatesPopup();
            if (_modUpdatesPopup == null) return;

            string path = Path.Combine(Config._DataPath, ModUpdatesCsvName);
            string body = LoadAndFormatModUpdatesCsv(path);
            _modUpdatesBodyText.text = body;
            _modUpdatesPopup.SetActive(true);
        }

        /// <summary>
        /// Loads modupdates.csv, parses it with Serializer.CSV, and returns a formatted string for display.
        /// First row is treated as header; comment lines (#) are skipped.
        /// </summary>
        private static string LoadAndFormatModUpdatesCsv(string path)
        {
            if (!File.Exists(path))
            {
                Melon<TweaksAndFixes>.Logger.Warning($"Mod Updates: CSV not found at {path}. Create TAFData/modupdates.csv to show updates.");
                return LocalizeManager.Localize("$TAF_Ui_MainMenu_ModUpdates_NoFile") ?? "No modupdates.csv found. Add TAFData/modupdates.csv to display mod updates.";
            }

            var rows = Serializer.CSV.ParseToRows(File.ReadAllText(path, Encoding.UTF8), skipCommentLines: true);
            if (rows == null || rows.Count == 0)
                return LocalizeManager.Localize("$TAF_Ui_MainMenu_ModUpdates_Empty") ?? "No entries in modupdates.csv.";

            var sb = new StringBuilder();
            string[] header = rows[0];
            int colCount = header.Length;

            // Header line
            sb.AppendLine(string.Join("  |  ", header));
            sb.AppendLine(new string('-', 60));

            for (int r = 1; r < rows.Count; r++)
            {
                string[] cells = rows[r];
                for (int c = 0; c < cells.Length && c < colCount; c++)
                {
                    if (c > 0) sb.Append("  |  ");
                    sb.Append(cells[c].Trim());
                }
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private static void EnsureModUpdatesPopup()
        {
            if (_modUpdatesPopup != null) return;

            GameObject template = ModUtils.GetChildAtPath("Global/Ui/UiMain/Popup/Generic");
            GameObject popupRoot = ModUtils.GetChildAtPath("Global/Ui/UiMain/Popup");
            if (template == null || popupRoot == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("Mod Updates: Could not find Generic popup or Popup root.");
                return;
            }

            _modUpdatesPopup = GameObject.Instantiate(template);
            _modUpdatesPopup.transform.SetParent(popupRoot.transform, false);
            _modUpdatesPopup.name = "TAF_ModUpdates_Popup";
            _modUpdatesPopup.transform.SetScale(1, 1, 1);
            _modUpdatesPopup.transform.localPosition = Vector3.zero;
            RectTransform rect = _modUpdatesPopup.GetComponent<RectTransform>();
            if (rect != null) { rect.offsetMin = Vector3.zero; rect.offsetMax = Vector3.zero; }

            if (_modUpdatesPopup.GetChild("BgScreen") != null)
                _modUpdatesPopup.GetChild("BgScreen").TryDestroy();

            GameObject window = _modUpdatesPopup.GetChild("Window");
            if (window == null) return;

            // Use scrollable text (TextScrollView) so long content is readable
            GameObject textScrollView = window.GetChild("TextScrollView");
            GameObject textOld = window.GetChild("TextOld");
            if (textScrollView != null)
            {
                textScrollView.SetActive(true);
                // Increase height of the text scroll box so more content is visible
                RectTransform scrollRect = textScrollView.GetComponent<RectTransform>();
                if (scrollRect != null)
                {
                    Vector2 sd = scrollRect.sizeDelta;
                    scrollRect.sizeDelta = new Vector2(sd.x, Mathf.Max(320f, sd.y));
                }
                GameObject content = ModUtils.GetChildAtPath("Viewport/Content", textScrollView);
                GameObject textObj = content != null ? content.GetChild("Text", true) : null;
                if (textObj == null) textObj = content?.GetChild("Text (TMP)", true);
                _modUpdatesBodyText = textObj != null ? textObj.GetComponent<TMP_Text>() : null;
            }
            if (_modUpdatesBodyText == null && textOld != null)
            {
                textOld.SetActive(true);
                _modUpdatesBodyText = textOld.GetComponent<TMP_Text>();
            }
            if (_modUpdatesBodyText == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("Mod Updates: Could not find body TMP_Text in Generic popup.");
                return;
            }
            // Smaller font so more content fits in the scroll area
            _modUpdatesBodyText.fontSize = Mathf.Max(12, _modUpdatesBodyText.fontSize * 0.75f);

            GameObject headerObj = window.GetChild("Header");
            if (headerObj != null)
            {
                TMP_Text headerText = headerObj.GetComponent<TMP_Text>();
                if (headerText != null)
                    headerText.text = LocalizeManager.Localize("$TAF_Ui_MainMenu_ModUpdates") ?? "Mod Updates";
            }

            // Hide Yes/No, keep or add single Ok/Close
            ModUtils.GetChildAtPath("Buttons/Yes", window).TryDestroy();
            ModUtils.GetChildAtPath("Buttons/No", window).TryDestroy();
            GameObject okBtn = ModUtils.GetChildAtPath("Buttons/Ok", window);
            if (okBtn != null)
            {
                okBtn.SetActive(true);
                if (okBtn.TryGetComponent(out Button okButton))
                {
                    okButton.onClick.RemoveAllListeners();
                    okButton.onClick.AddListener(new System.Action(() => _modUpdatesPopup.SetActive(false)));
                }
                GameObject okTextObj = okBtn.GetChild("Text (TMP)", true);
                if (okTextObj != null)
                {
                    okTextObj.TryDestroyComponent<LocalizeText>();
                    TMP_Text okTmp = okTextObj.GetComponent<TMP_Text>();
                    if (okTmp != null) okTmp.text = LocalizeManager.Localize("$Ui_Popup_Generic_Ok") ?? "OK";
                }
            }

            _modUpdatesPopup.SetActive(false);
        }

        private static void OnTAFUpdatesClick()
        {
            // TODO: Open TAF Updates UI / flow
            Melon<TweaksAndFixes>.Logger.Msg("TAF Updates clicked.");
        }
    }
}
