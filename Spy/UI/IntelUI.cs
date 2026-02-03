using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;

namespace Spy
{
    /// <summary>
    /// Campaign UI: "Intel" button that opens a window listing available Spies.
    /// </summary>
    public static class IntelUI
    {
        private const string CampaignTopPanelPath = "Global/Ui/UiMain/WorldEx/TopPanel/Tabs/Buttons";

        private static bool _initialized;
        private static GameObject? _windowRoot;
        private static GameObject? _buttonRoot;
        private static GameObject? _spiesListPanel;
        private static GameObject? _spiesContent;
        private static GameObject? _intelEstimatesContent;
        private static SpyActor? _selectedSpyActor;

        /// <summary>Intel level per country name (e.g. "United States" -> "Limited"). Default "Unknown".</summary>
        private static readonly Dictionary<string, string> IntelLevelByCountry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void Initialize()
        {
            if (_initialized)
                return;

            CreateOpenButton();
            CreateWindow();

            _initialized = true;
            MelonLogger.Msg("[IntelUI] Initialized.");
        }

        private static void CreateOpenButton()
        {
            GameObject? buttonsParent = TweaksAndFixes.ModUtils.GetChildAtPath(CampaignTopPanelPath);
            if (buttonsParent == null)
            {
                MelonLogger.Warning("[IntelUI] Campaign buttons parent not found.");
                return;
            }

            GameObject? templateButton = buttonsParent.GetChild("Fleet");
            if (templateButton == null)
            {
                MelonLogger.Warning("[IntelUI] Fleet button template not found.");
                return;
            }

            GameObject buttonObj = UnityEngine.Object.Instantiate(templateButton);
            buttonObj.transform.SetParent(buttonsParent.transform, false);
            buttonObj.name = "Intel";
            buttonObj.transform.localScale = Vector3.one;
            buttonObj.transform.localPosition = Vector3.zero;

            GameObject? textObj = buttonObj.GetChild("Text (TMP)", true);
            if (textObj != null)
            {
                DestroyComponent<LocalizeText>(textObj);
                TMP_Text? buttonText = textObj.GetComponent<TMP_Text>();
                if (buttonText != null)
                    buttonText.text = "Intel";
            }

            Button? button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(new Action(() => ShowWindow()));
            }

            _buttonRoot = buttonObj;
        }

        private static void CreateWindow()
        {
            if (G.ui?.FleetWindow?.Root == null)
            {
                MelonLogger.Warning("[IntelUI] FleetWindow not available for cloning.");
                return;
            }

            _windowRoot = UnityEngine.Object.Instantiate(G.ui.FleetWindow.Root);
            _windowRoot.name = "IntelWindow";
            _windowRoot.transform.SetParent(G.ui.gameObject.transform, false);
            _windowRoot.transform.localScale = Vector3.one;
            _windowRoot.transform.localPosition = Vector3.zero;
            _windowRoot.SetActive(false);

            var cfw = _windowRoot.GetComponent<CampaignFleetWindow>();
            if (cfw != null)
                UnityEngine.Object.Destroy(cfw);

            GameObject? root = _windowRoot.GetChild("Root");
            if (root == null)
            {
                MelonLogger.Warning("[IntelUI] Window root not found.");
                return;
            }

            // Fixed-size root so background doesn't overflow (no stretch, no empty space below content)
            RectTransform rootRect = root.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.sizeDelta = new Vector2(1600f, 720f);
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            // Remove every original child of root so no ship-building / fleet UI shows (destroy to avoid stray text/layout)
            while (root.transform.childCount > 0)
            {
                Transform t = root.transform.GetChild(0);
                if (t != null && t.gameObject != null)
                    UnityEngine.Object.Destroy(t.gameObject);
            }
            // Hide any other direct children of the window so no Fleet window title/header leaks
            if (_windowRoot != null)
            {
                for (int i = _windowRoot.transform.childCount - 1; i >= 0; i--)
                {
                    Transform t = _windowRoot.transform.GetChild(i);
                    if (t != null && t.gameObject != null && t.gameObject != root)
                        t.gameObject.SetActive(false);
                }
            }

            CreateIntelEstimatesPanel(root);
            CreateSpiesListPanel(root);
            CreateIntelBottomButtons(root);
            CreateCloseButton(root);
        }

        private const float IntelEstimatesPanelWidth = 300f;

        private static void CreateIntelEstimatesPanel(GameObject root)
        {
            GameObject panel = new GameObject("Intel Estimates Panel");
            panel.transform.SetParent(root.transform, false);
            panel.transform.SetAsLastSibling();
            panel.transform.localScale = Vector3.one;
            panel.SetActive(true);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            // Anchor to left edge only (fixed width), full height – so panel doesn't stretch or shrink the table
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.offsetMin = new Vector2(0f, -360f);
            panelRect.offsetMax = new Vector2(IntelEstimatesPanelWidth, -70f);
            LayoutElement le = panel.AddComponent<LayoutElement>();
            le.preferredWidth = IntelEstimatesPanelWidth;
            le.flexibleWidth = 0f;
            le.minWidth = IntelEstimatesPanelWidth;

            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.14f, 0.14f, 0.16f, 0.95f);

            GameObject title = new GameObject("Title");
            title.transform.SetParent(panel.transform, false);
            RectTransform titleRect = title.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(8f, -32f);
            titleRect.offsetMax = new Vector2(-8f, -4f);
            TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.text = "Intel Estimates";
            titleText.fontSize = 16f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;

            GameObject scrollObj = new GameObject("Scroll View");
            scrollObj.transform.SetParent(panel.transform, false);
            RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(4f, 4f);
            scrollRect.offsetMax = new Vector2(-4f, -36f);

            ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _intelEstimatesContent = content;

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            content.AddComponent<VerticalLayoutGroup>().spacing = 2f;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
        }

        private static void CreateSpiesListPanel(GameObject root)
        {
            // Always use our own contained panel: fixed rect, header at top, scroll view below with Mask so list scrolls
            CreateSpiesListPanelFallback(root);
        }

        private static void CreateSpiesColumnHeader(GameObject tablePanel)
        {
            if (tablePanel == null) return;

            GameObject columnHeader = new GameObject("Spies Column Header");
            columnHeader.transform.SetParent(tablePanel.transform, false);
            columnHeader.transform.SetAsLastSibling(); // draw on top of scroll area

            RectTransform colHeaderRect = columnHeader.AddComponent<RectTransform>();
            colHeaderRect.anchorMin = new Vector2(0f, 1f);
            colHeaderRect.anchorMax = new Vector2(1f, 1f);
            colHeaderRect.pivot = new Vector2(0.5f, 1f);
            colHeaderRect.offsetMin = new Vector2(0f, -SpiesHeaderHeight);
            colHeaderRect.offsetMax = new Vector2(0f, 0f);
            LayoutElement headerLe = columnHeader.AddComponent<LayoutElement>();
            headerLe.preferredHeight = SpiesHeaderHeight;
            headerLe.flexibleHeight = 0f;

            HorizontalLayoutGroup colHlg = columnHeader.AddComponent<HorizontalLayoutGroup>();
            colHlg.spacing = 6f;
            colHlg.childForceExpandHeight = true;
            colHlg.childForceExpandWidth = false;
            colHlg.childControlWidth = true;
            RectOffset pad = colHlg.padding;
            pad.left = 8; pad.right = 8; pad.top = 4; pad.bottom = 4;
            colHlg.padding = pad;
            AddHeaderColumn(columnHeader, "Name", ColName);
            AddHeaderColumn(columnHeader, "Yrs", ColYrsActive);
            AddHeaderColumn(columnHeader, "Success", ColSuccess);
            AddHeaderColumn(columnHeader, "Failed", ColFailed);
            AddHeaderColumn(columnHeader, "Exp", ColExpLvl);
            AddHeaderColumn(columnHeader, "Sneak", ColSneakiness);
            AddHeaderColumn(columnHeader, "Eff", ColEfficiency);
            AddHeaderColumn(columnHeader, "Plan", ColPlanning);
            AddHeaderColumn(columnHeader, "Status", ColStatus);
        }

        private const float SpiesHeaderHeight = 36f;

        private static void CreateSpiesListPanelFallback(GameObject root)
        {
            GameObject panel = new GameObject("Spies List");
            panel.transform.SetParent(root.transform, false);
            panel.transform.SetAsLastSibling();
            panel.transform.localScale = Vector3.one;
            panel.SetActive(true);
            _spiesListPanel = panel;

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.offsetMin = new Vector2(IntelEstimatesPanelWidth + 8f, -360f);
            panelRect.offsetMax = new Vector2(-20f, -70f);

            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.11f, 0.11f, 0.13f, 0.98f);

            // Scroll view first (below header), then header drawn on top
            GameObject scrollObj = new GameObject("Scroll View");
            scrollObj.transform.SetParent(panel.transform, false);
            RectTransform scrollRectTrans = scrollObj.AddComponent<RectTransform>();
            scrollRectTrans.anchorMin = new Vector2(0f, 0f);
            scrollRectTrans.anchorMax = new Vector2(1f, 1f);
            scrollRectTrans.offsetMin = new Vector2(4f, 4f);
            scrollRectTrans.offsetMax = new Vector2(-4f, -(SpiesHeaderHeight + 4f));

            ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 1f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _spiesContent = content;

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.content = contentRect;
            scroll.viewport = viewportRect;

            // Column header at top of container (drawn on top of scroll)
            CreateSpiesColumnHeader(panel);
        }

        private static void CreateIntelBottomButtons(GameObject root)
        {
            GameObject? buttonBarTemplate = G.ui?.FleetWindow?.DesignButtonsRoot;
            if (buttonBarTemplate == null)
                buttonBarTemplate = TweaksAndFixes.ModUtils.GetChildAtPath("Global/Ui/UiMain/WorldEx/Windows/Fleet Window/Root/Design Buttons");

            GameObject buttonBar;
            if (buttonBarTemplate != null)
            {
                buttonBar = UnityEngine.Object.Instantiate(buttonBarTemplate);
                buttonBar.name = "Intel Buttons";
                buttonBar.transform.SetParent(root.transform, false);
                buttonBar.transform.SetAsLastSibling();
                buttonBar.transform.localScale = Vector3.one;
                buttonBar.SetActive(true);

                RectTransform barRect = buttonBar.GetComponent<RectTransform>();
                if (barRect != null)
                {
                    barRect.anchorMin = new Vector2(0f, 0f);
                    barRect.anchorMax = new Vector2(1f, 0f);
                    barRect.offsetMin = new Vector2(IntelEstimatesPanelWidth + 8f, 20f);
                    barRect.offsetMax = new Vector2(-20f, 55f);
                }

                var children = buttonBar.GetChildren();
                if (children != null)
                {
                    foreach (GameObject child in children)
                        UnityEngine.Object.Destroy(child);
                }
            }
            else
            {
                buttonBar = new GameObject("Intel Buttons");
                buttonBar.transform.SetParent(root.transform, false);
                buttonBar.transform.SetAsLastSibling();
                RectTransform barRect = buttonBar.AddComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0f, 0f);
                barRect.anchorMax = new Vector2(1f, 0f);
                barRect.offsetMin = new Vector2(IntelEstimatesPanelWidth + 8f, 20f);
                barRect.offsetMax = new Vector2(-20f, 55f);
                HorizontalLayoutGroup hlg = buttonBar.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 10f;
                hlg.childForceExpandHeight = true;
            }

            CreateActionButton(buttonBar, "Begin Mission", OnBeginMission);
            CreateActionButton(buttonBar, "Recruit Spy", OnRecruitSpy);
            CreateActionButton(buttonBar, "Abandon Spy", OnAbandonSpy);
            CreateActionButton(buttonBar, "Negotiate Release", OnNegotiateRelease);
        }

        private static void CreateActionButton(GameObject parent, string text, Action onClick)
        {
            GameObject? buttonTemplate = null;
            if (G.ui?.FleetWindow?.DesignButtonsRoot != null)
            {
                buttonTemplate = G.ui.FleetWindow.DesignButtonsRoot.GetChild("Build Ship", true);
                if (buttonTemplate == null)
                {
                    var children = G.ui.FleetWindow.DesignButtonsRoot.GetChildren();
                    if (children != null && children.Count > 0)
                        buttonTemplate = children[0];
                }
            }

            if (buttonTemplate == null)
            {
                buttonTemplate = new GameObject("ButtonTemplate");
                buttonTemplate.AddComponent<RectTransform>();
                buttonTemplate.AddComponent<Image>();
                buttonTemplate.AddComponent<Button>();
                GameObject textObj = new GameObject("Text (TMP)");
                textObj.transform.SetParent(buttonTemplate.transform, false);
                textObj.AddComponent<TextMeshProUGUI>().text = "Button";
            }

            GameObject button = UnityEngine.Object.Instantiate(buttonTemplate);
            button.name = text;
            button.transform.SetParent(parent.transform, false);
            button.transform.localScale = Vector3.one;

            DestroyComponent<LocalizeText>(button);
            GameObject? textObj2 = button.GetChild("Text (TMP)", true);
            if (textObj2 != null)
            {
                DestroyComponent<LocalizeText>(textObj2);
                var buttonText = textObj2.GetComponent<TMP_Text>();
                if (buttonText != null)
                    buttonText.text = text;
            }

            Button? btn = button.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(onClick);
            }
        }

        private static void OnBeginMission() { /* TODO */ }

        private static void OnRecruitSpy()
        {
            int nextNum = SpyActor.All.Count + 1;
            string name = "Agent " + nextNum;
            var spy = new SpyActor(name, 0, 0, 0, 1, 50, 50, 50, SpyActor.StatusAvailable);
            SpyActor.All.Add(spy);
            _selectedSpyActor = spy;
            RefreshSpiesList();
            MelonLogger.Msg($"[IntelUI] Recruited {name}.");
        }

        private static void OnAbandonSpy()
        {
            if (_selectedSpyActor == null)
            {
                MelonLogger.Msg("[IntelUI] Select a spy to abandon.");
                return;
            }
            // Only allow abandoning spies who are Available or Captured (not On Mission or Transiting)
            if (_selectedSpyActor.Status != SpyActor.StatusAvailable && _selectedSpyActor.Status != SpyActor.StatusCaptured)
            {
                MelonLogger.Msg($"[IntelUI] Cannot abandon {_selectedSpyActor.Name}: must be Available or Captured (current: {_selectedSpyActor.Status}).");
                return;
            }
            string name = _selectedSpyActor.Name;
            SpyActor.All.Remove(_selectedSpyActor);
            _selectedSpyActor = null;
            RefreshSpiesList();
            MelonLogger.Msg($"[IntelUI] Abandoned {name}.");
        }

        private static void OnNegotiateRelease() { /* TODO */ }

        private static void SelectSpy(SpyActor actor)
        {
            _selectedSpyActor = actor;
            RefreshSpiesList();
        }

        private static void CreateCloseButton(GameObject root)
        {
            GameObject? buttonTemplate = null;
            if (G.ui?.FleetWindow?.DesignButtonsRoot != null)
            {
                var children = G.ui.FleetWindow.DesignButtonsRoot.GetChildren();
                if (children != null && children.Count > 0)
                    buttonTemplate = children[0];
            }

            if (buttonTemplate == null)
            {
                buttonTemplate = new GameObject("ButtonTemplate");
                buttonTemplate.AddComponent<RectTransform>();
                buttonTemplate.AddComponent<Image>();
                buttonTemplate.AddComponent<Button>();
                GameObject textObj = new GameObject("Text (TMP)");
                textObj.transform.SetParent(buttonTemplate.transform, false);
                textObj.AddComponent<TextMeshProUGUI>().text = "X";
            }

            GameObject closeButton = UnityEngine.Object.Instantiate(buttonTemplate);
            closeButton.name = "Close";
            closeButton.transform.SetParent(root.transform, false);
            closeButton.transform.SetAsLastSibling();
            closeButton.transform.localScale = Vector3.one;
            closeButton.SetActive(true);

            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.offsetMax = new Vector2(-10f, -10f);
            closeRect.offsetMin = new Vector2(-50f, -50f);

            GameObject? closeTextObj = closeButton.GetChild("Text (TMP)", true);
            if (closeTextObj != null)
            {
                DestroyComponent<LocalizeText>(closeTextObj);
                var closeText = closeTextObj.GetComponent<TMP_Text>();
                if (closeText != null)
                {
                    closeText.text = "X";
                    closeText.fontSize = 20f;
                    closeText.alignment = TextAlignmentOptions.Center;
                }
            }

            Button? closeBtn = closeButton.GetComponent<Button>();
            if (closeBtn != null)
            {
                closeBtn.onClick.RemoveAllListeners();
                closeBtn.onClick.AddListener(new Action(HideWindow));
            }
        }

        private static void ShowWindow()
        {
            if (_windowRoot != null)
            {
                _windowRoot.SetActive(true);
                RefreshSpiesList();
                RefreshIntelEstimates();
            }
        }

        private static string GetIntelLevel(string countryName)
        {
            return IntelLevelByCountry.TryGetValue(countryName, out string? level) ? level : "Unknown";
        }

        /// <summary>Set intel level for a country (e.g. "Limited", "Infiltrated"). Call from missions or UI.</summary>
        public static void SetIntelLevel(string countryName, string level)
        {
            IntelLevelByCountry[countryName] = level;
        }

        private static void RefreshIntelEstimates()
        {
            if (_intelEstimatesContent == null) return;

            // Clear existing rows (avoid Il2Cpp foreach cast – use index loop)
            int childCount = _intelEstimatesContent.transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform t = _intelEstimatesContent.transform.GetChild(i);
                if (t != null && t.gameObject != null)
                    UnityEngine.Object.Destroy(t.gameObject);
            }

            if (CampaignController.Instance == null || CampaignController.Instance.CampaignData?.Players == null)
            {
                AddIntelEstimateRow(_intelEstimatesContent, "—", "Not in campaign");
                return;
            }

            // Only active/playable countries: major players that are not disabled (same filter as campaign logic)
            foreach (Player player in CampaignController.Instance.CampaignData.Players)
            {
                if (player == null || player.isDisabled || !player.isMajor) continue;
                string countryName = player.Name(false);
                string level = GetIntelLevel(countryName);
                AddIntelEstimateRow(_intelEstimatesContent, countryName, level);
            }
        }

        private static void AddIntelEstimateRow(GameObject content, string countryName, string level)
        {
            GameObject row = new GameObject($"Intel_{countryName}");
            row.transform.SetParent(content.transform, false);
            row.transform.localScale = Vector3.one;

            LayoutElement le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;
            le.flexibleWidth = 1f;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(row.transform, false);
            LayoutElement textLe = textObj.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1f;
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"{countryName}: {level}";
            tmp.fontSize = 13f;
            tmp.alignment = TextAlignmentOptions.Left;
        }

        private static void HideWindow()
        {
            if (_windowRoot != null)
                _windowRoot.SetActive(false);
        }

        private static void AddHeaderColumn(GameObject parent, string label, float width)
        {
            GameObject go = new GameObject(label);
            go.transform.SetParent(parent.transform, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = 0f;
            le.minWidth = Math.Min(width, 40f);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.overflowMode = TextOverflowModes.Truncate;
        }

        // Column widths (must match header)
        private const float ColName = 130f;
        private const float ColYrsActive = 72f;
        private const float ColSuccess = 64f;
        private const float ColFailed = 56f;
        private const float ColExpLvl = 64f;
        private const float ColSneakiness = 72f;
        private const float ColEfficiency = 72f;
        private const float ColPlanning = 72f;
        private const float ColStatus = 88f;

        private static void RefreshSpiesList()
        {
            if (_spiesContent == null) return;

            // Clear existing rows (keep Template if present, so cloned list structure stays intact)
            int n = _spiesContent.transform.childCount;
            for (int i = n - 1; i >= 0; i--)
            {
                Transform t = _spiesContent.transform.GetChild(i);
                if (t != null && t.gameObject != null && t.gameObject.name != "Template" && !t.gameObject.name.Contains("Template"))
                    UnityEngine.Object.Destroy(t.gameObject);
            }

            var actors = SpyActor.All;
            if (actors.Count == 0)
            {
                AddEmptyRow(_spiesContent, "No spies available.");
                return;
            }

            foreach (SpyActor actor in actors)
                AddSpyActorRow(_spiesContent, actor);
        }

        private static void AddEmptyRow(GameObject content, string message)
        {
            GameObject row = new GameObject("EmptyRow");
            row.transform.SetParent(content.transform, false);
            row.transform.localScale = Vector3.one;
            row.AddComponent<LayoutElement>().preferredHeight = 28f;
            AddRowText(row, message, 600f);
        }

        private static void AddSpyActorRow(GameObject content, SpyActor actor)
        {
            GameObject row = new GameObject($"SpyRow_{actor.Name}");
            row.transform.SetParent(content.transform, false);
            row.transform.localScale = Vector3.one;

            LayoutElement le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 26f;
            le.flexibleWidth = 1f;

            // Background (for selection highlight and button hit area)
            Image bg = row.AddComponent<Image>();
            bg.color = actor == _selectedSpyActor ? new Color(0.25f, 0.35f, 0.5f, 0.9f) : new Color(0.18f, 0.18f, 0.2f, 0.85f);
            bg.raycastTarget = true;

            Button rowBtn = row.AddComponent<Button>();
            rowBtn.onClick.RemoveAllListeners();
            rowBtn.onClick.AddListener(new Action(() => SelectSpy(actor)));

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;
            var pad = hlg.padding;
            pad.left = 8; pad.right = 8; pad.top = 2; pad.bottom = 2;
            hlg.padding = pad;

            AddRowText(row, actor.Name, ColName);
            AddRowText(row, actor.YrsActive.ToString(), ColYrsActive);
            AddRowText(row, actor.SuccessMissions.ToString(), ColSuccess);
            AddRowText(row, actor.FailedMissions.ToString(), ColFailed);
            AddRowText(row, actor.ExperienceLevel.ToString(), ColExpLvl);
            AddRowText(row, actor.Sneakiness.ToString(), ColSneakiness);
            AddRowText(row, actor.Efficiency.ToString(), ColEfficiency);
            AddRowText(row, actor.Planning.ToString(), ColPlanning);
            AddRowText(row, actor.Status, ColStatus);
        }

        private static void AddRowText(GameObject parent, string text, float width)
        {
            GameObject go = new GameObject("Text");
            go.transform.SetParent(parent.transform, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = 0f;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 13f;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.overflowMode = TextOverflowModes.Truncate;
        }

        private static void DestroyComponent<T>(GameObject go) where T : UnityEngine.Object
        {
            var c = go.GetComponent<T>();
            if (c != null)
                UnityEngine.Object.Destroy(c);
        }
    }
}
