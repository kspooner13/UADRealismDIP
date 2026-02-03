using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using TweaksAndFixes.Data;
using TweaksAndFixes.Harmony;

namespace TweaksAndFixes
{
    /// <summary>
    /// Fleet Composition UI for Campaign - allows creating fleet composition templates
    /// and ordering ships to match those compositions.
    /// </summary>
    public static class FleetCompositionUI
    {
        private static bool _initialized;
        private static GameObject? _windowRoot;
        private static GameObject? _buttonRoot;
        private static TAFUI.TAF_Button? _openButton;

        /// <summary>
        /// Path to campaign top panel buttons where we'll add the Fleet Composition button.
        /// </summary>
        public const string CampaignTopPanelPath = "Global/Ui/UiMain/WorldEx/TopPanel/Tabs/Buttons";

        /// <summary>
        /// Initialize the Fleet Composition UI. Call from ApplyCampaignWindowModifications().
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            CreateOpenButton();
            CreateWindow();

            _initialized = true;
            Melon<TweaksAndFixes>.Logger.Msg("[FleetCompositionUI] Initialized.");
        }

        /// <summary>
        /// Cleanup and allow re-initialization.
        /// </summary>
        public static void Destroy()
        {
            if (_windowRoot != null)
            {
                UnityEngine.Object.Destroy(_windowRoot);
                _windowRoot = null;
            }

            if (_openButton != null && _openButton.root != null)
            {
                UnityEngine.Object.Destroy(_openButton.root);
                _openButton = null;
            }

            _buttonRoot = null;
            _initialized = false;
        }

        private static void CreateOpenButton()
        {
            // Find the campaign top panel buttons
            GameObject buttonsParent = ModUtils.GetChildAtPath(CampaignTopPanelPath);
            if (buttonsParent == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Campaign buttons parent not found.");
                return;
            }

            // Clone an existing button (e.g., Fleet button) as template
            GameObject templateButton = buttonsParent.GetChild("Fleet");
            if (templateButton == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Fleet button template not found.");
                return;
            }

            GameObject buttonObj = GameObject.Instantiate(templateButton);
            buttonObj.transform.SetParent(buttonsParent.transform, false);
            buttonObj.name = "Fleet Composition";
            buttonObj.transform.localScale = Vector3.one;
            buttonObj.transform.localPosition = Vector3.zero;

            // Update button text - remove LocalizeText and set text directly
            GameObject textObj = buttonObj.GetChild("Text (TMP)", true);
            if (textObj != null)
            {
                // Remove LocalizeText component so we can set text directly
                textObj.TryDestroyComponent<LocalizeText>();
                
                // Set text directly
                TMP_Text buttonText = textObj.GetComponent<TMP_Text>();
                if (buttonText != null)
                {
                    buttonText.text = LocalizeManager.Localize("$TAF_Ui_FleetComposition_Button");
                }
            }

            // Set up button click
            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(new System.Action(() => {
                    ShowWindow();
                }));
            }

            _buttonRoot = buttonObj;
        }

        private static void CreateWindow()
        {
            // Clone FleetWindow structure as template
            if (G.ui?.FleetWindow?.Root == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] FleetWindow not available for cloning.");
                return;
            }

            _windowRoot = GameObject.Instantiate(G.ui.FleetWindow.Root);
            _windowRoot.name = "FleetCompositionWindow";
            _windowRoot.transform.SetParent(G.ui.gameObject.transform, false);
            _windowRoot.transform.localScale = Vector3.one;
            _windowRoot.transform.localPosition = Vector3.zero;
            _windowRoot.SetActive(false);

            // Remove CampaignFleetWindow component
            _windowRoot.TryDestroyComponent<CampaignFleetWindow>();

            // Get root content
            GameObject root = _windowRoot.GetChild("Root");
            if (root == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Window root not found.");
                return;
            }

            // Set up window size to match Fleet window
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.offsetMax = new Vector2(800f, 400f);
            rootRect.offsetMin = new Vector2(-800f, -400f);

            // Hide unwanted tabs/elements
            GameObject designTab = root.GetChild("Design Ships", true);
            if (designTab != null) designTab.SetActive(false);
            GameObject fleetTab = root.GetChild("Fleet Ships", true);
            if (fleetTab != null) fleetTab.SetActive(false);
            GameObject designHeader = root.GetChild("Design Header", true);
            if (designHeader != null) designHeader.SetActive(false);
            GameObject fleetHeader = root.GetChild("Fleet Header", true);
            if (fleetHeader != null) fleetHeader.SetActive(false);
            GameObject designButtons = root.GetChild("Design Buttons", true);
            if (designButtons != null) designButtons.SetActive(false);
            GameObject fleetButtons = root.GetChild("Fleet Buttons", true);
            if (fleetButtons != null) fleetButtons.SetActive(false);
            GameObject designInfo = root.GetChild("Design Ship Info", true);
            if (designInfo != null) designInfo.SetActive(false);

            // Create Composition Ships list (right side, similar to Design Ships)
            CreateCompositionListPanel(root);

            // Create Composition Details panel (left side, similar to Design Ship Info)
            CreateCompositionDetailsPanel(root);

            // Create Composition Header (top of list)
            CreateCompositionHeader(root);

            // Create Action Buttons (bottom)
            CreateActionButtons(root);

            // Create Close button (top-right corner)
            CreateCloseButton(root);
        }

        private static void CreateCloseButton(GameObject root)
        {
            // Clone a button template for the close button
            GameObject? buttonTemplate = null;
            if (G.ui?.FleetWindow?.DesignButtonsRoot != null)
            {
                var children = G.ui.FleetWindow.DesignButtonsRoot.GetChildren();
                if (children != null && children.Count > 0)
                {
                    buttonTemplate = children[0];
                }
            }

            if (buttonTemplate == null)
            {
                // Fallback: create button from scratch
                buttonTemplate = new GameObject("ButtonTemplate");
                buttonTemplate.AddComponent<RectTransform>();
                buttonTemplate.AddComponent<Image>();
                buttonTemplate.AddComponent<Button>();
                GameObject textObj = new GameObject("Text (TMP)");
                textObj.transform.SetParent(buttonTemplate.transform, false);
                var textComp = textObj.AddComponent<TextMeshProUGUI>();
                textComp.text = "X";
            }

            GameObject closeButton = GameObject.Instantiate(buttonTemplate);
            closeButton.name = "Close";
            closeButton.transform.SetParent(root.transform, false);
            closeButton.transform.localScale = Vector3.one;
            closeButton.SetActive(true);

            // Position in top-right corner
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.offsetMax = new Vector2(-10f, -10f);
            closeRect.offsetMin = new Vector2(-50f, -50f);

            // Remove localization and set text
            closeButton.TryDestroyComponent<LocalizeText>();
            GameObject? closeTextObj = closeButton.GetChild("Text (TMP)", true);
            if (closeTextObj != null)
            {
                closeTextObj.TryDestroyComponent<LocalizeText>();
                var closeText = closeTextObj.GetComponent<TMP_Text>();
                if (closeText != null)
                {
                    closeText.text = "X";
                    closeText.fontSize = 20f;
                    closeText.alignment = TextAlignmentOptions.Center;
                }
            }

            // Set up click handler
            Button? closeBtn = closeButton.GetComponent<Button>();
            if (closeBtn != null)
            {
                closeBtn.onClick.RemoveAllListeners();
                closeBtn.onClick.AddListener(new System.Action(() => {
                    HideWindow();
                }));
            }
        }

        private static GameObject? _compositionListPanel;
        private static GameObject? _compositionDetailsPanel;
        private static GameObject? _compositionHeader;
        private static GameObject? _actionButtonsRoot;
        private static FleetCompositionData.Composition? _selectedComposition;

        private static void CreateCompositionListPanel(GameObject root)
        {
            // Clone Design Ships structure from FleetWindow
            GameObject? designShipsTemplate = null;
            if (G.ui?.FleetWindow?.Root != null)
            {
                designShipsTemplate = G.ui.FleetWindow.Root.GetChild("Root")?.GetChild("Design Ships", true);
            }
            
            if (designShipsTemplate == null)
            {
                designShipsTemplate = ModUtils.GetChildAtPath("Global/Ui/UiMain/WorldEx/Windows/Fleet Window/Root/Design Ships");
            }

            if (designShipsTemplate == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Design Ships template not found.");
                return;
            }

            _compositionListPanel = GameObject.Instantiate(designShipsTemplate);
            _compositionListPanel.name = "Composition Ships";
            _compositionListPanel.transform.SetParent(root.transform, false);
            _compositionListPanel.transform.localScale = Vector3.one;
            _compositionListPanel.SetActive(true);

            RectTransform listRect = _compositionListPanel.GetComponent<RectTransform>();
            listRect.offsetMax = new Vector2(-350f, -100f);
            listRect.offsetMin = new Vector2(-1200f, -700f);
        }

        private static void CreateCompositionDetailsPanel(GameObject root)
        {
            // Clone Design Ship Info structure from FleetWindow
            GameObject? designInfoTemplate = null;
            if (G.ui?.FleetWindow?.DesignShipInfoRoot != null)
            {
                designInfoTemplate = G.ui.FleetWindow.DesignShipInfoRoot;
            }
            
            if (designInfoTemplate == null)
            {
                designInfoTemplate = ModUtils.GetChildAtPath("Global/Ui/UiMain/WorldEx/Windows/Fleet Window/Root/Design Ship Info");
            }

            if (designInfoTemplate == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Design Ship Info template not found.");
                return;
            }

            _compositionDetailsPanel = GameObject.Instantiate(designInfoTemplate);
            _compositionDetailsPanel.name = "Composition Details";
            _compositionDetailsPanel.transform.SetParent(root.transform, false);
            _compositionDetailsPanel.transform.localScale = Vector3.one;
            _compositionDetailsPanel.SetActive(true);

            RectTransform detailsRect = _compositionDetailsPanel.GetComponent<RectTransform>();
            detailsRect.offsetMax = new Vector2(350f, -100f);
            detailsRect.offsetMin = new Vector2(0f, -700f);

            // Update text content
            GameObject? textInfo = _compositionDetailsPanel.GetChild("Text", true)?.GetChild("ShipTextInfo", true);
            if (textInfo != null)
            {
                TMP_Text? textComp = textInfo.GetComponent<TMP_Text>();
                if (textComp != null)
                {
                    textComp.text = LocalizeManager.Localize("$TAF_Ui_FleetComposition_SelectDetails");
                }
            }
        }

        private static void CreateCompositionHeader(GameObject root)
        {
            // Clone Design Header structure from FleetWindow
            GameObject? designHeaderTemplate = null;
            if (G.ui?.FleetWindow?.DesignHeader != null)
            {
                designHeaderTemplate = G.ui.FleetWindow.DesignHeader.gameObject;
            }
            
            if (designHeaderTemplate == null)
            {
                designHeaderTemplate = ModUtils.GetChildAtPath("Global/Ui/UiMain/WorldEx/Windows/Fleet Window/Root/Design Header");
            }

            if (designHeaderTemplate == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Design Header template not found.");
                return;
            }

            _compositionHeader = GameObject.Instantiate(designHeaderTemplate);
            _compositionHeader.name = "Composition Header";
            _compositionHeader.transform.SetParent(root.transform, false);
            _compositionHeader.transform.localScale = Vector3.one;
            _compositionHeader.SetActive(true);

            RectTransform headerRect = _compositionHeader.GetComponent<RectTransform>();
            headerRect.offsetMax = new Vector2(-350f, 0f);
            headerRect.offsetMin = new Vector2(-1200f, -44.4f);

            // Reuse existing Fleet header columns (TMP components are already set up).
            // We'll keep 4 columns and repurpose them to: Name, BB, CA, DD.
            var cols = _compositionHeader.GetChildren();
            if (cols == null || cols.Count == 0)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Header has no children to reuse.");
                return;
            }

            GameObject colName = FindFirstChild(cols, "Name") ?? cols[0];
            GameObject colBB = FindFirstChild(cols, "Year") ?? (cols.Count > 1 ? cols[1] : colName);
            GameObject colCA = FindFirstChild(cols, "Cost") ?? (cols.Count > 2 ? cols[2] : colName);
            GameObject colDD = FindFirstChild(cols, "Tonnes", "Tonnage") ?? (cols.Count > 3 ? cols[3] : colName);

            foreach (var col in cols)
                col.SetActive(false);

            ConfigureHeaderColumn(colName, "Name", 300f);
            ConfigureHeaderColumn(colBB, "BB", 60f);
            ConfigureHeaderColumn(colCA, "CA", 60f);
            ConfigureHeaderColumn(colDD, "DD", 60f);
        }

        private static void CreateHeaderColumn(GameObject parent, string text, float width)
        {
            GameObject col = new GameObject(text);
            col.transform.SetParent(parent.transform, false);
            col.transform.localScale = Vector3.one;

            RectTransform colRect = col.AddComponent<RectTransform>();
            LayoutElement le = col.AddComponent<LayoutElement>();
            le.preferredWidth = width;

            // TMP_Text is abstract; use TextMeshProUGUI if we ever hit this fallback path.
            var textComp = col.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 14f;
            textComp.alignment = TextAlignmentOptions.Left;
        }

        private static GameObject? FindFirstChild(Il2CppSystem.Collections.Generic.List<GameObject> children, params string[] names)
        {
            for (int ni = 0; ni < names.Length; ni++)
            {
                string n = names[ni];
                for (int i = 0; i < children.Count; i++)
                {
                    var c = children[i];
                    if (c != null && c.name == n)
                        return c;
                }
            }
            return null;
        }

        private static void ConfigureHeaderColumn(GameObject col, string label, float width)
        {
            if (col == null) return;
            col.SetActive(true);

            var le = col.GetComponent<LayoutElement>();
            if (le == null) le = col.AddComponent<LayoutElement>();
            le.preferredWidth = width;

            // Try to find the TMP text for this header column
            GameObject textObj = col.GetChild("Text (TMP)", true) ?? col;
            textObj.TryDestroyComponent<LocalizeText>();
            col.TryDestroyComponent<LocalizeText>();

            var tmp = textObj.GetComponent<TMP_Text>();
            if (tmp != null)
                tmp.text = label;
        }

        private static void CreateActionButtons(GameObject root)
        {
            // Clone Design Buttons structure from FleetWindow
            GameObject? designButtonsTemplate = null;
            if (G.ui?.FleetWindow?.DesignButtonsRoot != null)
            {
                designButtonsTemplate = G.ui.FleetWindow.DesignButtonsRoot;
            }
            
            if (designButtonsTemplate == null)
            {
                designButtonsTemplate = ModUtils.GetChildAtPath("Global/Ui/UiMain/WorldEx/Windows/Fleet Window/Root/Design Buttons");
            }

            if (designButtonsTemplate == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Design Buttons template not found.");
                return;
            }

            _actionButtonsRoot = GameObject.Instantiate(designButtonsTemplate);
            _actionButtonsRoot.name = "Composition Buttons";
            _actionButtonsRoot.transform.SetParent(root.transform, false);
            _actionButtonsRoot.transform.localScale = Vector3.one;
            _actionButtonsRoot.SetActive(true);

            RectTransform buttonsRect = _actionButtonsRoot.GetComponent<RectTransform>();
            buttonsRect.offsetMax = new Vector2(-350f, 50f);
            buttonsRect.offsetMin = new Vector2(-1200f, 31.1f);

            // Clear existing buttons
            var buttonChildren = _actionButtonsRoot.GetChildren();
            if (buttonChildren != null)
            {
                foreach (GameObject child in buttonChildren)
                {
                    UnityEngine.Object.Destroy(child);
                }
            }

            // Create action buttons
            CreateActionButton(_actionButtonsRoot, LocalizeManager.Localize("$TAF_Ui_FleetComposition_NewComposition"), OnCreateComposition);
            CreateActionButton(_actionButtonsRoot, LocalizeManager.Localize("$TAF_Ui_FleetComposition_Edit"), OnEditComposition);
            CreateActionButton(_actionButtonsRoot, LocalizeManager.Localize("$TAF_Ui_FleetComposition_Delete"), OnDeleteSelectedComposition);
            CreateActionButton(_actionButtonsRoot, LocalizeManager.Localize("$TAF_Ui_FleetComposition_GatherFleet"), OnGatherSelectedComposition);
            CreateActionButton(_actionButtonsRoot, LocalizeManager.Localize("$TAF_Ui_FleetComposition_BuildFleet"), OnBuildSelectedComposition);
        }

        private static void CreateActionButton(GameObject parent, string text, System.Action onClick)
        {
            // Clone a button template from FleetWindow
            GameObject? buttonTemplate = null;
            if (G.ui?.FleetWindow?.DesignButtonsRoot != null)
            {
                buttonTemplate = G.ui.FleetWindow.DesignButtonsRoot.GetChild("Build Ship", true);
                if (buttonTemplate == null)
                {
                    var children = G.ui.FleetWindow.DesignButtonsRoot.GetChildren();
                    if (children != null && children.Count > 0)
                    {
                        buttonTemplate = children[0];
                    }
                }
            }
            
            if (buttonTemplate == null)
            {
                // Fallback: create button from scratch
                buttonTemplate = new GameObject("ButtonTemplate");
                buttonTemplate.AddComponent<RectTransform>();
                buttonTemplate.AddComponent<Image>();
                Button btnTemplate = buttonTemplate.AddComponent<Button>();
                GameObject textObj = new GameObject("Text (TMP)");
                textObj.transform.SetParent(buttonTemplate.transform, false);
                // TMP_Text is abstract; use TextMeshProUGUI.
                var textComp = textObj.AddComponent<TextMeshProUGUI>();
                textComp.text = "Button";
            }

            GameObject button = GameObject.Instantiate(buttonTemplate);
            button.name = text;
            button.transform.SetParent(parent.transform, false);
            button.transform.localScale = Vector3.one;

            // IMPORTANT: cloned buttons often have LocalizeText that will overwrite our label.
            // Remove localization components and then set the label text directly.
            button.TryDestroyComponent<LocalizeText>();
            GameObject? textObj2 = button.GetChild("Text (TMP)", true);
            if (textObj2 != null)
            {
                textObj2.TryDestroyComponent<LocalizeText>();
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

        private static void ShowWindow()
        {
            if (_windowRoot != null)
            {
                _windowRoot.SetActive(true);
                RefreshCompositionList();
                _selectedComposition = null;
                if (_compositionDetailsPanel != null)
                {
                    GameObject textInfo = _compositionDetailsPanel.GetChild("Text", true)?.GetChild("ShipTextInfo", true);
                    if (textInfo != null)
                    {
                        TMP_Text textComp = textInfo.GetComponent<TMP_Text>();
                        if (textComp != null)
                        {
                            textComp.text = LocalizeManager.Localize("$TAF_Ui_FleetComposition_SelectDetails");
                        }
                    }
                }
            }
        }

        private static void HideWindow()
        {
            if (_windowRoot != null)
            {
                _windowRoot.SetActive(false);
            }
        }

        private static void RefreshCompositionList()
        {
            if (_windowRoot == null || _compositionListPanel == null) return;

            // Find the scroll view content
            GameObject scrollView = _compositionListPanel.GetChild("ScrollRect", true);
            if (scrollView == null) scrollView = _compositionListPanel.GetChild("Scroll View", true);
            if (scrollView == null) return;

            GameObject viewport = scrollView.GetChild("Viewport");
            if (viewport == null) return;

            GameObject content = viewport.GetChild("Content", true);
            if (content == null) content = viewport.GetChild("Cont", true);
            if (content == null) return;

            // Clear existing items
            var contentChildren = content.GetChildren();
            if (contentChildren != null)
            {
                foreach (GameObject child in contentChildren)
                {
                    if (child.name != "Template" && !child.name.Contains("Template"))
                    {
                        UnityEngine.Object.Destroy(child);
                    }
                }
            }

            // Get saved compositions
            var compositions = FleetCompositionData.LoadAll();

            // Create UI items for each composition
            foreach (var comp in compositions)
            {
                CreateCompositionListItem(content, comp);
            }

            // If no compositions, show message
            if (compositions.Count == 0)
            {
                var emptyText = new TAFUI.TAF_Text(
                    content,
                    "EmptyText",
                    LocalizeManager.Localize("$TAF_Ui_FleetComposition_Empty"),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f)
                );
                if (!emptyText.root.TryGetComponent<LayoutElement>(out _))
                    emptyText.root.AddComponent<LayoutElement>().preferredHeight = 60f;
            }
        }

        private static void CreateCompositionListItem(GameObject parent, FleetCompositionData.Composition comp)
        {
            // Reuse the existing Fleet list row template so the look matches perfectly.
            GameObject? template = parent.GetChild("Template", true);
            if (template == null)
            {
                // Fallback to old behavior if template not present (should be rare).
                GameObject itemFallback = new GameObject($"Composition_{comp.Name}");
                itemFallback.transform.SetParent(parent.transform, false);
                itemFallback.transform.localScale = Vector3.one;

                var le = itemFallback.AddComponent<LayoutElement>();
                le.preferredHeight = 30f;
                le.flexibleWidth = 1f;

                var hlg = itemFallback.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 5f;
                var pad = hlg.padding;
                pad.left = 10; pad.right = 10; pad.top = 2; pad.bottom = 2;
                hlg.padding = pad;

                CreateListItemColumn(itemFallback, comp.Name, 300f);
                CreateListItemColumn(itemFallback, comp.Battleships.ToString(), 60f);
                CreateListItemColumn(itemFallback, comp.Cruisers.ToString(), 60f);
                CreateListItemColumn(itemFallback, comp.Destroyers.ToString(), 60f);
                return;
            }

            GameObject row = GameObject.Instantiate(template);
            row.name = $"Composition_{comp.Name}";
            row.transform.SetParent(parent.transform, false);
            row.transform.localScale = Vector3.one;
            row.SetActive(true);

            // Remove ship-specific scripts from the template (avoid it trying to bind to a Ship)
            row.TryDestroyComponent<FleetWindow_ShipElementUI>();

            // The clickable area in the Fleet template is usually a child named "Button".
            // Bind our selection handler to that button (or fall back to the row root).
            GameObject clickTarget = row.GetChild("Button", true) ?? row;
            var btn = clickTarget.GetComponent<Button>() ?? row.GetComponent<Button>() ?? clickTarget.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(new System.Action(() => SelectComposition(comp)));

            // Hide all columns except Name/Year/Cost/Tonnes and repurpose those as our 4 fields.
            var rowCols = clickTarget.GetChildren();
            if (rowCols != null)
            {
                foreach (var c in rowCols)
                    c.SetActive(false);
            }

            GameObject rName = clickTarget.GetChild("Name", true) ?? row;
            GameObject rBB = clickTarget.GetChild("Year", true) ?? row;
            GameObject rCA = clickTarget.GetChild("Cost", true) ?? row;
            GameObject rDD = clickTarget.GetChild("Tonnes", true) ?? clickTarget.GetChild("Tonnage", true) ?? row;

            ConfigureRowColumn(rName, comp.Name, 300f);
            ConfigureRowColumn(rBB, comp.Battleships.ToString(), 60f);
            ConfigureRowColumn(rCA, comp.Cruisers.ToString(), 60f);
            ConfigureRowColumn(rDD, comp.Destroyers.ToString(), 60f);
        }

        private static void CreateListItemColumn(GameObject parent, string text, float width)
        {
            GameObject col = new GameObject($"Col_{text}");
            col.transform.SetParent(parent.transform, false);
            col.transform.localScale = Vector3.one;

            RectTransform colRect = col.AddComponent<RectTransform>();
            LayoutElement le = col.AddComponent<LayoutElement>();
            le.preferredWidth = width;

            // TMP_Text is abstract; use TextMeshProUGUI if we ever hit this fallback path.
            var textComp = col.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 12f;
            textComp.alignment = TextAlignmentOptions.Left;
        }

        private static void ConfigureRowColumn(GameObject col, string value, float width)
        {
            if (col == null) return;
            col.SetActive(true);

            var le = col.GetComponent<LayoutElement>();
            if (le == null) le = col.AddComponent<LayoutElement>();
            le.preferredWidth = width;

            GameObject textObj = col.GetChild("Text (TMP)", true) ?? col;
            textObj.TryDestroyComponent<LocalizeText>();
            col.TryDestroyComponent<LocalizeText>();

            var tmp = textObj.GetComponent<TMP_Text>();
            if (tmp != null)
                tmp.text = value;
        }

        private static void SelectComposition(FleetCompositionData.Composition comp)
        {
            _selectedComposition = comp;
            UpdateCompositionDetails(comp);
            Melon<TweaksAndFixes>.Logger.Msg($"[FleetCompositionUI] Selected composition: {comp.Name}");
        }

        private static void UpdateCompositionDetails(FleetCompositionData.Composition comp)
        {
            if (_compositionDetailsPanel == null || comp == null) return;

            GameObject textInfo = _compositionDetailsPanel.GetChild("Text", true)?.GetChild("ShipTextInfo", true);
            if (textInfo != null)
            {
                TMP_Text textComp = textInfo.GetComponent<TMP_Text>();
                if (textComp != null)
                {
                    string details = $"{comp.Name}:\n\n";
                    details += $"Battleships: {comp.Battleships}\n";
                    details += $"Cruisers: {comp.Cruisers}\n";
                    details += $"Destroyers: {comp.Destroyers}\n";
                    if (!string.IsNullOrEmpty(comp.Description))
                    {
                        details += $"\n{comp.Description}";
                    }
                    textComp.text = details;
                }
            }
        }

        private static void OnEditComposition()
        {
            if (_selectedComposition == null)
            {
                Melon<TweaksAndFixes>.Logger.Msg("[FleetCompositionUI] No composition selected for editing.");
                return;
            }
            // TODO: Open edit dialog
            Melon<TweaksAndFixes>.Logger.Msg($"[FleetCompositionUI] Edit composition: {_selectedComposition.Name}");
        }

        private static void OnDeleteSelectedComposition()
        {
            if (_selectedComposition == null)
            {
                Melon<TweaksAndFixes>.Logger.Msg("[FleetCompositionUI] No composition selected for deletion.");
                return;
            }
            OnDeleteComposition(_selectedComposition);
            _selectedComposition = null;
            if (_compositionDetailsPanel != null)
            {
                GameObject textInfo = _compositionDetailsPanel.GetChild("Text", true)?.GetChild("ShipTextInfo", true);
                if (textInfo != null)
                {
                    TMP_Text textComp = textInfo.GetComponent<TMP_Text>();
                    if (textComp != null)
                    {
                        textComp.text = LocalizeManager.Localize("$TAF_Ui_FleetComposition_SelectDetails");
                    }
                }
            }
        }

        private static void OnGatherSelectedComposition()
        {
            if (_selectedComposition == null)
            {
                Melon<TweaksAndFixes>.Logger.Msg("[FleetCompositionUI] No composition selected.");
                return;
            }
            OnGatherFleet(_selectedComposition);
        }

        private static void OnBuildSelectedComposition()
        {
            if (_selectedComposition == null)
            {
                Melon<TweaksAndFixes>.Logger.Msg("[FleetCompositionUI] No composition selected.");
                return;
            }
            OnBuildFleet(_selectedComposition);
        }

        private static void OnCreateComposition()
        {
            Melon<TweaksAndFixes>.Logger.Msg("[FleetCompositionUI] Create composition clicked.");
            // TODO: Open create/edit dialog
            // For now, create a default composition
            var comp = new FleetCompositionData.Composition
            {
                Name = "Battle Fleet",
                Battleships = 4,
                Cruisers = 4,
                Destroyers = 8
            };
            FleetCompositionData.Save(comp);
            RefreshCompositionList();
        }

        private static void OnGatherFleet(FleetCompositionData.Composition comp)
        {
            Melon<TweaksAndFixes>.Logger.Msg($"[FleetCompositionUI] Gather fleet for composition: {comp.Name}");
            
            if (!GameManager.IsWorld || CampaignController.Instance == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Not in campaign world.");
                return;
            }

            // Get player's available ships
            Player? player = ExtraGameData.MainPlayer();
            if (player == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Player not found.");
                return;
            }

            // Get available ships from fleet window
            var availableShips = GetAvailableShips(player);
            
            // Select ships to match composition
            var selectedShips = SelectShipsForComposition(availableShips, comp);
            
            if (selectedShips.Count == 0)
            {
                Melon<TweaksAndFixes>.Logger.Msg(LocalizeManager.Localize("$TAF_Ui_FleetComposition_NoShips"));
                return;
            }

            // Show port selection dialog
            ShowPortSelectionDialog(selectedShips, comp);
        }

        private static void OnBuildFleet(FleetCompositionData.Composition comp)
        {
            Melon<TweaksAndFixes>.Logger.Msg($"[FleetCompositionUI] Build fleet for composition: {comp.Name}");
            
            if (!GameManager.IsWorld || CampaignController.Instance == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Not in campaign world.");
                return;
            }

            Player? player = ExtraGameData.MainPlayer();
            if (player == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Player not found.");
                return;
            }

            // Start construction for ships matching the composition
            StartConstructionForComposition(player, comp);
        }

        private static void OnDeleteComposition(FleetCompositionData.Composition comp)
        {
            FleetCompositionData.Delete(comp.Name);
            RefreshCompositionList();
        }

        private static List<Ship> GetAvailableShips(Player player)
        {
            var ships = new List<Ship>();
            
            if (G.ui?.FleetWindow?.fleetUiByShip == null)
                return ships;

            foreach (var entry in G.ui.FleetWindow.fleetUiByShip)
            {
                Ship ship = entry.Value.CurrentShip;
                if (ship != null && 
                    ship.player == player && 
                    !ship.IsInSea &&
                    !ship.isBuilding &&
                    !ship.isRefit &&
                    !ship.isCommissioning)
                {
                    ships.Add(ship);
                }
            }

            return ships;
        }

        private static List<Ship> SelectShipsForComposition(List<Ship> availableShips, FleetCompositionData.Composition comp)
        {
            // Sort ships by type
            var battleships = availableShips.Where(s => s.shipType.name == "bb" || s.shipType.name == "bc").ToList();
            var cruisers = availableShips.Where(s => s.shipType.name == "ca" || s.shipType.name == "cl").ToList();
            var destroyers = availableShips.Where(s => s.shipType.name == "dd").ToList();

            // Select ships to match composition
            var selectedShips = new List<Ship>();
            
            selectedShips.AddRange(battleships.Take(comp.Battleships));
            selectedShips.AddRange(cruisers.Take(comp.Cruisers));
            selectedShips.AddRange(destroyers.Take(comp.Destroyers));

            return selectedShips;
        }

        private static void ShowPortSelectionDialog(List<Ship> ships, FleetCompositionData.Composition comp)
        {
            // Get player's ports
            Player? player = ExtraGameData.MainPlayer();
            if (player == null || CampaignController.Instance == null)
                return;

            // Find port selection popup template
            GameObject portPopupTemplate = ModUtils.GetChildAtPath("Global/Ui/UiMain/WorldEx/PopWindows/PortPopupSmall");
            if (portPopupTemplate == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] Port popup template not found.");
                return;
            }

            // Create port selection dialog
            GameObject portDialog = GameObject.Instantiate(portPopupTemplate);
            portDialog.name = "FleetCompositionPortSelection";
            portDialog.transform.SetParent(G.ui.gameObject.transform, false);
            portDialog.transform.localScale = Vector3.one;
            portDialog.transform.localPosition = Vector3.zero;
            portDialog.SetActive(true);

            // Get PortPopupUI component
            PortPopupUI? portPopup = portDialog.GetComponent<PortPopupUI>();
            if (portPopup == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] PortPopupUI component not found.");
                UnityEngine.Object.Destroy(portDialog);
                return;
            }

            // Get player's ports
            var playerPorts = new List<PortElement>();
            foreach (var port in CampaignMap.PortsDb.Ports)
            {
                if (port.CurrentProvince != null && port.CurrentProvince.ControllerPlayer == player)
                {
                    playerPorts.Add(port);
                }
            }

            if (playerPorts.Count == 0)
            {
                Melon<TweaksAndFixes>.Logger.Msg("[FleetCompositionUI] Player has no ports.");
                UnityEngine.Object.Destroy(portDialog);
                return;
            }

            // Show first port as default (user can change)
            PortElement defaultPort = playerPorts[0];
            portPopup.Show(defaultPort);

            // Override the port selection to move ships
            // Note: This is a simplified approach - you may need to hook into the actual port selection logic
            // For now, we'll select ships and let the user manually use ChangePort button
            SelectShipsInFleetWindow(ships);
            
            Melon<TweaksAndFixes>.Logger.Msg(String.Format(LocalizeManager.Localize("$TAF_Ui_FleetComposition_Gathered"), ships.Count, comp.Name, defaultPort.Name));
        }

        private static void SelectShipsInFleetWindow(List<Ship> ships)
        {
            if (G.ui?.FleetWindow != null)
            {
                G.ui.FleetWindow.selectedElements.Clear();
                
                foreach (var ship in ships)
                {
                    if (G.ui.FleetWindow.fleetUiByShip.TryGetValue(ship, out var uiElement))
                    {
                        if (!G.ui.FleetWindow.selectedElements.Contains(uiElement))
                        {
                            G.ui.FleetWindow.selectedElements.Add(uiElement);
                        }
                    }
                }
            }
        }

        private static void StartConstructionForComposition(Player player, FleetCompositionData.Composition comp)
        {
            if (G.ui?.FleetWindow == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[FleetCompositionUI] FleetWindow not available.");
                return;
            }

            // Get available designs from the design tab
            var designsToBuild = new List<Ship>();
            
            if (G.ui.FleetWindow.designUiByShip != null)
            {
                foreach (var entry in G.ui.FleetWindow.designUiByShip)
                {
                    Ship design = entry.Value.CurrentShip;
                    if (design == null || design.player != player)
                        continue;

                    string shipType = design.shipType.name;
                    bool matchesComposition = false;
                    int needed = 0;

                    if ((shipType == "bb" || shipType == "bc") && comp.Battleships > 0)
                    {
                        matchesComposition = true;
                        needed = comp.Battleships;
                    }
                    else if ((shipType == "ca" || shipType == "cl") && comp.Cruisers > 0)
                    {
                        matchesComposition = true;
                        needed = comp.Cruisers;
                    }
                    else if (shipType == "dd" && comp.Destroyers > 0)
                    {
                        matchesComposition = true;
                        needed = comp.Destroyers;
                    }

                    if (matchesComposition)
                    {
                        // Check if we can build this design
                        if (PlayerController.Instance != null && 
                            PlayerController.Instance.CanBuildShipsFromDesign(design, out _))
                        {
                            // Select the design and trigger build
                            G.ui.FleetWindow.selectedElements.Clear();
                            if (G.ui.FleetWindow.designUiByShip.TryGetValue(design, out var uiElement))
                            {
                                G.ui.FleetWindow.selectedElements.Add(uiElement);
                                
                                // Find Build button in DesignButtonsRoot
                                GameObject designButtonsRoot = G.ui.FleetWindow.DesignButtonsRoot;
                                if (designButtonsRoot != null)
                                {
                                    GameObject buildButtonObj = designButtonsRoot.GetChild("Build Ship", true);
                                    if (buildButtonObj == null)
                                        buildButtonObj = designButtonsRoot.GetChild("Build", true);
                                    
                                    if (buildButtonObj != null)
                                    {
                                        Button buildButton = buildButtonObj.GetComponent<Button>();
                                        if (buildButton != null && buildButton.interactable)
                                        {
                                            // Build one ship at a time up to needed count
                                            for (int i = 0; i < needed && i < 10; i++) // Limit to prevent spam
                                            {
                                                buildButton.onClick.Invoke();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Melon<TweaksAndFixes>.Logger.Msg(String.Format(LocalizeManager.Localize("$TAF_Ui_FleetComposition_BuildStarted"), comp.Name));
        }
    }
}
