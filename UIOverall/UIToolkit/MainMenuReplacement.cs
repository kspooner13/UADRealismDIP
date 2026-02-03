#if USE_UI_TOOLKIT

using System;
using UnityEngine;
using UnityEngine.UIElements;
using Il2Cpp;
using MelonLoader;
using UIOverall.Core;

namespace UIOverall.UIToolkit
{
    /// <summary>
    /// Replaces the game's main menu with a UI Toolkit panel. Shown when on main menu; hides the original MainMenu and displays a custom panel.
    /// </summary>
    public static class MainMenuReplacement
    {
        private static GameObject? _menuRoot;
        private static UIDocument? _uiDocument;
        private static bool _shown;

        /// <summary>Show the UI Toolkit main menu. Call when MainMenu/LevelMainMenu scene has loaded. Tries to find Ui in scene if not yet set.</summary>
        public static void Show()
        {
            if (_shown)
                return;
            UIOverhaulBase.TrySetUiFromScene();
            GameObject? root = UIOverhaulBase.UiRoot;
            if (root == null)
            {
                Canvas? canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
                if (canvas != null)
                    root = canvas.gameObject;
            }
            if (root == null)
            {
                Melon<global::UIOverall.UIOverallMod>.Logger.Warning("[UIOverall] No UI root or Canvas in scene; cannot show main menu replacement.");
                return;
            }

            try
            {
                CreateAndShowPanel(root);
                _shown = true;
                Melon<global::UIOverall.UIOverallMod>.Logger.Msg("[UIOverall] Main menu replaced with UI Toolkit panel.");
            }
            catch (Exception ex)
            {
                Melon<global::UIOverall.UIOverallMod>.Logger.Error($"[UIOverall] MainMenuReplacement.Show failed: {ex}");
            }
        }

        /// <summary>Hide our panel and optionally re-show the game's main menu (e.g. when leaving main menu).</summary>
        public static void Hide()
        {
            if (!_shown)
                return;
            _shown = false;
            if (_menuRoot != null)
            {
                UnityEngine.Object.Destroy(_menuRoot);
                _menuRoot = null;
                _uiDocument = null;
            }
        }

        private static void CreateAndShowPanel(GameObject parent)
        {
            _menuRoot = new GameObject("UIOverall_MainMenu");
            _menuRoot.transform.SetParent(parent.transform, worldPositionStays: false);
            _menuRoot.transform.localPosition = Vector3.zero;
            _menuRoot.transform.localScale = Vector3.one;

            _uiDocument = _menuRoot.AddComponent<UIDocument>();

            PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _uiDocument.panelSettings = panelSettings;

            VisualElement root = _uiDocument.rootVisualElement;
            root.name = "mainmenu-root";
            root.style.flexGrow = 1;
            root.style.width = new Length(100, LengthUnit.Percent);
            root.style.height = new Length(100, LengthUnit.Percent);
            root.style.backgroundColor = new Color(0.1f, 0.12f, 0.18f, 0.95f);
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            VisualElement container = new VisualElement();
            container.name = "mainmenu-container";
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            container.style.width = new Length(400, LengthUnit.Pixel);

            Label title = new Label("Ultimate Admiral:\nDreadnoughts");
            title.name = "mainmenu-title";
            title.style.fontSize = 32;
            title.style.color = new Color(0.9f, 0.9f, 0.95f);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 40;
            title.style.whiteSpace = WhiteSpace.Normal;
            container.Add(title);

            AddMenuButton(container, "New Game", OnNewGame);
            AddMenuButton(container, "Continue", OnContinue);
            AddMenuButton(container, "Settings", OnSettings);
            AddMenuButton(container, "Quit", OnQuit);

            root.Add(container);
        }

        private static void AddMenuButton(VisualElement parent, string text, Action callback)
        {
            Button btn = new Button(callback) { text = text };
            btn.style.width = new Length(280, LengthUnit.Pixel);
            btn.style.height = 44;
            btn.style.fontSize = 18;
            btn.style.marginTop = 8;
            btn.style.marginBottom = 8;
            parent.Add(btn);
        }

        private static void OnNewGame()
        {
            Melon<global::UIOverall.UIOverallMod>.Logger.Msg("[UIOverall] New Game clicked — wire to game's new game flow (e.g. Ui or GameManager).");
            // TODO: call game's new game (e.g. G.ui.StartNewGame or similar; find via dnSpy)
        }

        private static void OnContinue()
        {
            Melon<global::UIOverall.UIOverallMod>.Logger.Msg("[UIOverall] Continue clicked — wire to game's continue/load flow.");
            // TODO: call game's continue (e.g. G.ui.Continue or load save)
        }

        private static void OnSettings()
        {
            Melon<global::UIOverall.UIOverallMod>.Logger.Msg("[UIOverall] Settings clicked — wire to game's options/settings.");
            // TODO: call game's show settings (e.g. G.ui.ShowOptions)
        }

        private static void OnQuit()
        {
            Application.Quit();
        }
    }
}

#endif
