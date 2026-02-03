using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Il2Cpp;
using MelonLoader;

namespace TweaksAndFixes
{
    /// <summary>
    /// Compact armor protection panel showing ship image with colored zone overlays + armor values.
    /// Also includes a Calculations fold on the left side for gun selection.
    /// </summary>
    public static class ConstructorArmorProtectionPanel
    {
        private static float _referencePenetrationMm = 250f;
        private static string _selectedGunName = null; // null = use default 250mm
        private static GameObject _foldRoot;
        private static GameObject _imageContainer;
        private static RawImage _shipImage;
        private static Text _armorText;
        private static bool _initialized;

        // Left side Calculations fold
        private static GameObject _calcFoldRoot;
        private static InputField _penInputField;
        private static Text _penDisplayText;
        private static bool _calcInitialized;

        // Cached texture references
        private static RawImage _gameSectionsSideRawImage;
        private static RawImage _gameSectionsTopRawImage;

        // Zone overlay boxes - now a list since we have multiple boxes per zone
        private static readonly List<(Ship.A zone, Image box, Text label)> _zoneBoxes = new List<(Ship.A, Image, Text)>();

        // Zone grid - creates multiple boxes per armor zone to match the game's section grid
        // Each entry: zone (for armor value), x, y, width, height (fractions 0-1 of image)
        private static readonly (Ship.A zone, float x, float y, float w, float h)[] _zoneGrid = new[]
        {
            // Belt row - multiple sections along waterline (stern to bow, left to right)
            (Ship.A.BeltStern,  0.12f, 0.38f, 0.08f, 0.09f),
            (Ship.A.BeltStern,  0.21f, 0.38f, 0.08f, 0.09f),
            (Ship.A.Belt,       0.30f, 0.38f, 0.08f, 0.09f),
            (Ship.A.Belt,       0.39f, 0.38f, 0.08f, 0.09f),
            (Ship.A.Belt,       0.48f, 0.38f, 0.08f, 0.09f),
            (Ship.A.Belt,       0.57f, 0.38f, 0.08f, 0.09f),
            (Ship.A.Belt,       0.66f, 0.38f, 0.08f, 0.09f),
            (Ship.A.BeltBow,    0.75f, 0.38f, 0.08f, 0.09f),
            (Ship.A.BeltBow,    0.84f, 0.38f, 0.08f, 0.09f),

            // Deck row - above belt
            (Ship.A.DeckStern,  0.15f, 0.48f, 0.08f, 0.07f),
            (Ship.A.Deck,       0.24f, 0.48f, 0.08f, 0.07f),
            (Ship.A.Deck,       0.33f, 0.48f, 0.08f, 0.07f),
            (Ship.A.Deck,       0.42f, 0.48f, 0.08f, 0.07f),
            (Ship.A.Deck,       0.51f, 0.48f, 0.08f, 0.07f),
            (Ship.A.Deck,       0.60f, 0.48f, 0.08f, 0.07f),
            (Ship.A.Deck,       0.69f, 0.48f, 0.08f, 0.07f),
            (Ship.A.DeckBow,    0.78f, 0.48f, 0.08f, 0.07f),

            // Superstructure/turrets row
            (Ship.A.Barbette,       0.30f, 0.56f, 0.07f, 0.07f),
            (Ship.A.TurretSide,     0.40f, 0.56f, 0.07f, 0.07f),
            (Ship.A.Superstructure, 0.50f, 0.56f, 0.07f, 0.07f),
            (Ship.A.ConningTower,   0.58f, 0.56f, 0.07f, 0.07f),
            (Ship.A.TurretSide,     0.66f, 0.56f, 0.07f, 0.07f),
        };

        private static readonly Color Green = new Color(0.2f, 0.85f, 0.2f, 0.5f);
        private static readonly Color Yellow = new Color(0.95f, 0.85f, 0.1f, 0.5f);
        private static readonly Color Red = new Color(0.95f, 0.2f, 0.2f, 0.5f);
        private static readonly Color Gray = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        // Solid colors for text
        private static readonly Color GreenText = new Color(0.3f, 0.9f, 0.3f);
        private static readonly Color YellowText = new Color(0.95f, 0.85f, 0.2f);
        private static readonly Color RedText = new Color(0.95f, 0.3f, 0.3f);
        private static readonly Color GrayText = new Color(0.6f, 0.6f, 0.6f);

        public static void EnsurePanel(GameObject constructorRoot)
        {
            if (constructorRoot == null) return;
            if (!GameManager.IsConstructor) { Clear(); return; }

            // Ensure calculations fold on left side
            EnsureCalculationsFold();

            if (_initialized) { UpdatePanel(); return; }

            GameObject rightCont = ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor/Right/Scroll View/Viewport/Cont");
            if (rightCont == null) return;

            // Find FoldSectionsInfo to clone
            GameObject foldTemplate = null;
            for (int i = 0; i < rightCont.transform.childCount; i++)
            {
                var c = rightCont.transform.GetChild(i).gameObject;
                if (c.name == "FoldSectionsInfo")
                {
                    foldTemplate = c;
                    break;
                }
            }

            if (foldTemplate == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[ArmorProtectionPanel] FoldSectionsInfo not found");
                return;
            }

            // Clone the fold
            _foldRoot = GameObject.Instantiate(foldTemplate);
            _foldRoot.name = "TAF_FoldArmorProtection";
            _foldRoot.transform.SetParent(rightCont.transform, false);
            _foldRoot.transform.SetAsLastSibling();
            _foldRoot.SetActive(true);

            // Find and clear the content area, keeping the structure
            GameObject sectionsInfoCont = FindChildRecursive(_foldRoot, "SectionsInfoCont");
            if (sectionsInfoCont == null)
            {
                Melon<TweaksAndFixes>.Logger.Warning("[ArmorProtectionPanel] SectionsInfoCont not found in clone");
                GameObject.Destroy(_foldRoot);
                _foldRoot = null;
                return;
            }

            // Clear children
            for (int i = sectionsInfoCont.transform.childCount - 1; i >= 0; i--)
                GameObject.Destroy(sectionsInfoCont.transform.GetChild(i).gameObject);

            // Create content
            CreateContent(sectionsInfoCont);

            _initialized = true;
            CacheGameSectionsRawImages();
            UpdatePanel();
            Melon<TweaksAndFixes>.Logger.Msg("[ArmorProtectionPanel] Panel created");
        }

        private static void CreateContent(GameObject parent)
        {
            // Main container with vertical layout
            var container = new GameObject("Container");
            container.transform.SetParent(parent.transform, false);

            var containerRt = container.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.offsetMin = Vector2.zero;
            containerRt.offsetMax = Vector2.zero;

            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Image container (holds ship image + overlay boxes)
            _imageContainer = new GameObject("ImageContainer");
            _imageContainer.transform.SetParent(container.transform, false);

            var imgContRt = _imageContainer.AddComponent<RectTransform>();
            var imgContLE = _imageContainer.AddComponent<LayoutElement>();
            imgContLE.preferredHeight = 180f;
            imgContLE.flexibleWidth = 1f;

            // Ship image as background
            var imageGo = new GameObject("ShipImage");
            imageGo.transform.SetParent(_imageContainer.transform, false);

            var imageRt = imageGo.AddComponent<RectTransform>();
            imageRt.anchorMin = Vector2.zero;
            imageRt.anchorMax = Vector2.one;
            imageRt.offsetMin = Vector2.zero;
            imageRt.offsetMax = Vector2.zero;

            _shipImage = imageGo.AddComponent<RawImage>();
            _shipImage.color = new Color(0.2f, 0.25f, 0.3f, 1f);
            _shipImage.raycastTarget = false;

            // Create zone overlay boxes in a grid pattern
            Font gameFont = GetGameFont();
            int boxIndex = 0;
            foreach (var (zone, x, y, w, h) in _zoneGrid)
            {
                // Box
                var boxGo = new GameObject($"Box_{boxIndex}");
                boxGo.transform.SetParent(_imageContainer.transform, false);

                var boxRt = boxGo.AddComponent<RectTransform>();
                float halfW = w * 0.5f;
                float halfH = h * 0.5f;
                boxRt.anchorMin = new Vector2(x - halfW, y - halfH);
                boxRt.anchorMax = new Vector2(x + halfW, y + halfH);
                boxRt.offsetMin = Vector2.zero;
                boxRt.offsetMax = Vector2.zero;

                var boxImg = boxGo.AddComponent<Image>();
                boxImg.color = Gray;
                boxImg.raycastTarget = false;

                // Label inside box
                var labelGo = new GameObject($"Label_{boxIndex}");
                labelGo.transform.SetParent(boxGo.transform, false);

                var labelRt = labelGo.AddComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                var labelText = labelGo.AddComponent<Text>();
                labelText.font = gameFont;
                labelText.fontSize = 9;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.color = Color.white;
                labelText.text = "";
                labelText.raycastTarget = false;

                // Shadow for readability
                var shadow = labelGo.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
                shadow.effectDistance = new Vector2(1f, -1f);

                _zoneBoxes.Add((zone, boxImg, labelText));
                boxIndex++;
            }

            // Armor info text (compact, below image)
            var textGo = new GameObject("ArmorText");
            textGo.transform.SetParent(container.transform, false);

            var textRt = textGo.AddComponent<RectTransform>();
            var textLE = textGo.AddComponent<LayoutElement>();
            textLE.preferredHeight = 200f; // Increased for armor breakdown
            textLE.flexibleWidth = 1f;

            _armorText = textGo.AddComponent<Text>();
            _armorText.font = gameFont;
            _armorText.fontSize = 10;
            _armorText.alignment = TextAnchor.UpperLeft;
            _armorText.color = Color.white;
            _armorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _armorText.verticalOverflow = VerticalWrapMode.Truncate;
            _armorText.supportRichText = true; // Enable color tags
            _armorText.text = "";
        }

        private static void UpdatePanel()
        {
            UpdateShipImage();
            UpdateZoneBoxes();
            UpdateArmorText();
        }

        private static void UpdateShipImage()
        {
            if (_shipImage == null) return;

            Texture tex = null;

            // Try game's Sections RawImages first (Constructor)
            if (_gameSectionsSideRawImage != null && _gameSectionsSideRawImage.texture != null)
                tex = _gameSectionsSideRawImage.texture;
            else if (_gameSectionsTopRawImage != null && _gameSectionsTopRawImage.texture != null)
                tex = _gameSectionsTopRawImage.texture;

            // Try ConstructorSectionsCamera (dedicated constructor camera)
            if (tex == null)
            {
                var cam = GameObject.Find("ConstructorSectionsCamera");
                if (cam != null)
                {
                    var c = cam.GetComponent<Camera>();
                    if (c != null && c.targetTexture != null)
                    {
                        if (!c.enabled) c.Render();
                        tex = c.targetTexture;
                    }
                }
            }

            // Try SectionsCameraSide
            if (tex == null)
            {
                var cam = GameObject.Find("SectionsCameraSide");
                if (cam != null)
                {
                    var c = cam.GetComponent<Camera>();
                    if (c != null && c.targetTexture != null)
                    {
                        if (!c.enabled) c.Render();
                        tex = c.targetTexture;
                    }
                }
            }

            // Try DamageCameraSide (Battle mode)
            if (tex == null)
            {
                var cam = GameObject.Find("DamageCameraSide");
                if (cam != null)
                {
                    var c = cam.GetComponent<Camera>();
                    if (c != null && c.targetTexture != null)
                    {
                        if (!c.enabled) c.Render();
                        tex = c.targetTexture;
                    }
                }
            }

            _shipImage.texture = tex;
            _shipImage.color = tex != null ? Color.white : new Color(0.2f, 0.25f, 0.3f, 1f);
        }

        private static void UpdateZoneBoxes()
        {
            if (_zoneBoxes == null || _zoneBoxes.Count == 0) return;

            Ship ship = ShipM.GetActiveShip();
            if (ship == null || ship.armor == null) return;

            float armorQualityMod = GetArmorQualityModifier(ship);

            foreach (var (zone, boxImg, labelText) in _zoneBoxes)
            {
                if (boxImg == null) continue;

                float rawArmor = ModUtils.ArmorValue(ship.armor, zone);

                // Calculate effective armor with quality modifier
                float effectiveArmor = rawArmor * (1f + armorQualityMod / 100f);

                // Update box color based on effective armor vs reference penetration
                boxImg.color = GetBoxColor(effectiveArmor);

                // Update label - show effective value
                if (labelText != null)
                    labelText.text = effectiveArmor > 0 ? $"{effectiveArmor:F0}" : "";
            }
        }

        /// <summary>
        /// Get the armor quality modifier from ship's technologies (e.g., 100 for Krupp = +100%)
        /// </summary>
        private static float GetArmorQualityModifier(Ship ship)
        {
            if (ship == null || ship.techsActual == null) return 0f;

            float quality = 0f;
            foreach (var tech in ship.techsActual)
            {
                if (tech.effects != null && tech.effects.ContainsKey("armor_str"))
                {
                    var armorStrEffect = tech.effects["armor_str"];
                    if (armorStrEffect.Count > 0 && armorStrEffect[0].Count > 0)
                    {
                        if (float.TryParse(armorStrEffect[0][0],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float parsed))
                        {
                            quality += parsed;
                        }
                    }
                }
            }
            return quality;
        }

        private static Color GetBoxColor(float armor)
        {
            if (armor <= 0) return Gray;
            if (armor >= _referencePenetrationMm) return Green;
            if (armor >= _referencePenetrationMm * 0.6f) return Yellow;
            return Red;
        }

        private static void UpdateArmorText()
        {
            if (_armorText == null)
            {
                return;
            }

            Ship ship = ShipM.GetActiveShip();
            if (ship == null || ship.armor == null)
            {
                _armorText.text = "No ship selected";
                return;
            }

            float armorQualityMod = GetArmorQualityModifier(ship);
            float qualityMult = 1f + armorQualityMod / 100f;

            // Build armor breakdown text
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>vs {_referencePenetrationMm:F0}mm</b> | Quality: +{armorQualityMod:F0}%");
            sb.AppendLine();

            // Show each armor zone with raw → effective values
            sb.AppendLine("<b>Armor (Raw → Effective):</b>");

            // Belt zones
            float beltRaw = ModUtils.ArmorValue(ship.armor, Ship.A.Belt);
            float beltEff = beltRaw * qualityMult;
            sb.AppendLine($"Belt: {beltRaw:F0} → {ColorWrapValue(beltEff)}");

            float beltBowRaw = ModUtils.ArmorValue(ship.armor, Ship.A.BeltBow);
            float beltBowEff = beltBowRaw * qualityMult;
            sb.AppendLine($"Belt Bow: {beltBowRaw:F0} → {ColorWrapValue(beltBowEff)}");

            float beltSternRaw = ModUtils.ArmorValue(ship.armor, Ship.A.BeltStern);
            float beltSternEff = beltSternRaw * qualityMult;
            sb.AppendLine($"Belt Stern: {beltSternRaw:F0} → {ColorWrapValue(beltSternEff)}");

            // Deck zones
            float deckRaw = ModUtils.ArmorValue(ship.armor, Ship.A.Deck);
            float deckEff = deckRaw * qualityMult;
            sb.AppendLine($"Deck: {deckRaw:F0} → {ColorWrapValue(deckEff)}");

            float deckBowRaw = ModUtils.ArmorValue(ship.armor, Ship.A.DeckBow);
            float deckBowEff = deckBowRaw * qualityMult;
            sb.AppendLine($"Deck Bow: {deckBowRaw:F0} → {ColorWrapValue(deckBowEff)}");

            float deckSternRaw = ModUtils.ArmorValue(ship.armor, Ship.A.DeckStern);
            float deckSternEff = deckSternRaw * qualityMult;
            sb.AppendLine($"Deck Stern: {deckSternRaw:F0} → {ColorWrapValue(deckSternEff)}");

            // Turret/structure
            float turretSideRaw = ModUtils.ArmorValue(ship.armor, Ship.A.TurretSide);
            float turretSideEff = turretSideRaw * qualityMult;
            sb.AppendLine($"Turret Side: {turretSideRaw:F0} → {ColorWrapValue(turretSideEff)}");

            float turretTopRaw = ModUtils.ArmorValue(ship.armor, Ship.A.TurretTop);
            float turretTopEff = turretTopRaw * qualityMult;
            sb.AppendLine($"Turret Top: {turretTopRaw:F0} → {ColorWrapValue(turretTopEff)}");

            float barbetteRaw = ModUtils.ArmorValue(ship.armor, Ship.A.Barbette);
            float barbetteEff = barbetteRaw * qualityMult;
            sb.AppendLine($"Barbette: {barbetteRaw:F0} → {ColorWrapValue(barbetteEff)}");

            float ctRaw = ModUtils.ArmorValue(ship.armor, Ship.A.ConningTower);
            float ctEff = ctRaw * qualityMult;
            sb.AppendLine($"Conning Tower: {ctRaw:F0} → {ColorWrapValue(ctEff)}");

            float superRaw = ModUtils.ArmorValue(ship.armor, Ship.A.Superstructure);
            float superEff = superRaw * qualityMult;
            sb.Append($"Superstructure: {superRaw:F0} → {ColorWrapValue(superEff)}");

            _armorText.text = sb.ToString();
        }

        #region Calculations Fold (Left Side)

        private static void EnsureCalculationsFold()
        {
            if (_calcInitialized) return;

            GameObject leftCont = ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor/Left/Scroll View/Viewport/Cont");
            if (leftCont == null) return;

            // Create our own panel from scratch (don't clone FoldShipSettings - it breaks game UI)
            _calcFoldRoot = new GameObject("TAF_FoldCalculations");
            _calcFoldRoot.transform.SetParent(leftCont.transform, false);
            _calcFoldRoot.transform.SetSiblingIndex(2); // Place near top

            var rootRt = _calcFoldRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(1f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);

            var rootLE = _calcFoldRoot.AddComponent<LayoutElement>();
            rootLE.preferredHeight = 100f;
            rootLE.flexibleWidth = 1f;

            var rootVLG = _calcFoldRoot.AddComponent<VerticalLayoutGroup>();
            rootVLG.spacing = 4f;
            rootVLG.childAlignment = TextAnchor.UpperCenter;
            rootVLG.childControlWidth = true;
            rootVLG.childControlHeight = false;
            rootVLG.childForceExpandWidth = true;
            rootVLG.childForceExpandHeight = false;
            var pad = rootVLG.padding;
            pad.left = 5; pad.right = 5; pad.top = 5; pad.bottom = 5;
            rootVLG.padding = pad;

            // Add background
            var rootImg = _calcFoldRoot.AddComponent<Image>();
            rootImg.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);

            // Create content
            CreateCalculationsContent(_calcFoldRoot);

            _calcInitialized = true;
            Melon<TweaksAndFixes>.Logger.Msg("[ArmorProtectionPanel] Calculations panel created");
        }

        private static void CreateCalculationsContent(GameObject parent)
        {
            Font gameFont = GetGameFont();

            // Header
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(parent.transform, false);

            var headerLE = headerGo.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 20f;
            headerLE.flexibleWidth = 1f;

            var headerText = headerGo.AddComponent<Text>();
            headerText.font = gameFont;
            headerText.fontSize = 13;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color = new Color(0.9f, 0.9f, 0.7f);
            headerText.text = "Calculations";

            // Title label
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(parent.transform, false);

            var titleLE = titleGo.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 20f;
            titleLE.flexibleWidth = 1f;

            var titleText = titleGo.AddComponent<Text>();
            titleText.font = gameFont;
            titleText.fontSize = 11;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = Color.white;
            titleText.text = "Reference Penetration (mm):";

            // Input field container
            var inputContainer = new GameObject("InputContainer");
            inputContainer.transform.SetParent(parent.transform, false);

            var inputContLE = inputContainer.AddComponent<LayoutElement>();
            inputContLE.preferredHeight = 30f;
            inputContLE.flexibleWidth = 1f;

            var inputContHLG = inputContainer.AddComponent<HorizontalLayoutGroup>();
            inputContHLG.spacing = 5f;
            inputContHLG.childAlignment = TextAnchor.MiddleLeft;
            inputContHLG.childControlWidth = false;
            inputContHLG.childControlHeight = true;
            inputContHLG.childForceExpandWidth = false;
            inputContHLG.childForceExpandHeight = true;

            // Input field background
            var inputBg = new GameObject("InputBg");
            inputBg.transform.SetParent(inputContainer.transform, false);

            var inputBgLE = inputBg.AddComponent<LayoutElement>();
            inputBgLE.preferredWidth = 80f;
            inputBgLE.preferredHeight = 26f;

            var inputBgImg = inputBg.AddComponent<Image>();
            inputBgImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            // Input field
            var inputGo = new GameObject("InputField");
            inputGo.transform.SetParent(inputBg.transform, false);

            var inputRt = inputGo.AddComponent<RectTransform>();
            inputRt.anchorMin = Vector2.zero;
            inputRt.anchorMax = Vector2.one;
            inputRt.offsetMin = new Vector2(5f, 2f);
            inputRt.offsetMax = new Vector2(-5f, -2f);

            var inputTextGo = new GameObject("Text");
            inputTextGo.transform.SetParent(inputGo.transform, false);

            var inputTextRt = inputTextGo.AddComponent<RectTransform>();
            inputTextRt.anchorMin = Vector2.zero;
            inputTextRt.anchorMax = Vector2.one;
            inputTextRt.offsetMin = Vector2.zero;
            inputTextRt.offsetMax = Vector2.zero;

            var inputTextComp = inputTextGo.AddComponent<Text>();
            inputTextComp.font = gameFont;
            inputTextComp.fontSize = 12;
            inputTextComp.alignment = TextAnchor.MiddleLeft;
            inputTextComp.color = Color.white;
            inputTextComp.supportRichText = false;

            _penInputField = inputGo.AddComponent<InputField>();
            _penInputField.textComponent = inputTextComp;
            _penInputField.text = "250";
            _penInputField.contentType = InputField.ContentType.IntegerNumber;
            _penInputField.characterLimit = 4;

            // Apply button
            var applyBtn = new GameObject("ApplyBtn");
            applyBtn.transform.SetParent(inputContainer.transform, false);

            var applyLE = applyBtn.AddComponent<LayoutElement>();
            applyLE.preferredWidth = 60f;
            applyLE.preferredHeight = 26f;

            var applyImg = applyBtn.AddComponent<Image>();
            applyImg.color = new Color(0.3f, 0.4f, 0.3f, 1f);

            var applyBtnComp = applyBtn.AddComponent<Button>();
            applyBtnComp.onClick.AddListener(new System.Action(OnApplyPenetration));

            var applyTextGo = new GameObject("Text");
            applyTextGo.transform.SetParent(applyBtn.transform, false);

            var applyTextRt = applyTextGo.AddComponent<RectTransform>();
            applyTextRt.anchorMin = Vector2.zero;
            applyTextRt.anchorMax = Vector2.one;
            applyTextRt.offsetMin = Vector2.zero;
            applyTextRt.offsetMax = Vector2.zero;

            var applyText = applyTextGo.AddComponent<Text>();
            applyText.font = gameFont;
            applyText.fontSize = 11;
            applyText.alignment = TextAnchor.MiddleCenter;
            applyText.color = Color.white;
            applyText.text = "Apply";

            // Current value display
            var displayGo = new GameObject("Display");
            displayGo.transform.SetParent(parent.transform, false);

            var displayLE = displayGo.AddComponent<LayoutElement>();
            displayLE.preferredHeight = 20f;
            displayLE.flexibleWidth = 1f;

            _penDisplayText = displayGo.AddComponent<Text>();
            _penDisplayText.font = gameFont;
            _penDisplayText.fontSize = 11;
            _penDisplayText.alignment = TextAnchor.MiddleLeft;
            _penDisplayText.color = new Color(0.7f, 0.7f, 0.7f);
            _penDisplayText.text = $"Current: {_referencePenetrationMm:F0}mm";
        }

        private static void OnApplyPenetration()
        {
            if (_penInputField == null) return;

            if (float.TryParse(_penInputField.text, out float newPen) && newPen > 0)
            {
                _referencePenetrationMm = newPen;
                _selectedGunName = null; // Manual mode

                if (_penDisplayText != null)
                    _penDisplayText.text = $"Current: {_referencePenetrationMm:F0}mm";

                // Update the right panel display if initialized
                if (_initialized)
                {
                    UpdatePanel(); // Full panel update including image, boxes, and text
                    Melon<TweaksAndFixes>.Logger.Msg($"[ArmorProtectionPanel] Reference penetration set to {_referencePenetrationMm}mm, updated {_zoneBoxes.Count} boxes");
                }
                else
                {
                    Melon<TweaksAndFixes>.Logger.Warning($"[ArmorProtectionPanel] Reference penetration set to {_referencePenetrationMm}mm but panel not initialized");
                }
            }
        }

        #endregion

        /// <summary>
        /// Returns armor value with color formatting based on penetration comparison
        /// </summary>
        private static string ColorWrapValue(float armor)
        {
            if (armor <= 0) return $"<color=#{ColorToHex(GrayText)}>—</color>";

            Color c;
            if (armor >= _referencePenetrationMm) c = GreenText;
            else if (armor >= _referencePenetrationMm * 0.6f) c = YellowText;
            else c = RedText;

            return $"<color=#{ColorToHex(c)}>{armor:F0}mm</color>";
        }

        private static string ColorWrap(float armor)
        {
            if (armor <= 0) return $"<color=#{ColorToHex(GrayText)}>—</color>";

            Color c;
            if (armor >= _referencePenetrationMm) c = GreenText;
            else if (armor >= _referencePenetrationMm * 0.6f) c = YellowText;
            else c = RedText;

            return $"<color=#{ColorToHex(c)}>{armor:F0}</color>";
        }

        private static string ColorToHex(Color color)
        {
            int r = Mathf.RoundToInt(color.r * 255);
            int g = Mathf.RoundToInt(color.g * 255);
            int b = Mathf.RoundToInt(color.b * 255);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        private static void CacheGameSectionsRawImages()
        {
            GameObject foldSections = ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor/Right/Scroll View/Viewport/Cont/FoldSectionsInfo");
            if (foldSections == null) return;

            var sectionsSide = FindChildRecursive(foldSections, "SectionsSide");
            if (sectionsSide != null)
                _gameSectionsSideRawImage = sectionsSide.GetComponent<RawImage>();

            var sectionsTop = FindChildRecursive(foldSections, "SectionsTop");
            if (sectionsTop != null)
                _gameSectionsTopRawImage = sectionsTop.GetComponent<RawImage>();
        }

        private static GameObject FindChildRecursive(GameObject root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var found = FindChildRecursive(root.transform.GetChild(i).gameObject, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Font _cachedFont;
        private static Font GetGameFont()
        {
            if (_cachedFont != null) return _cachedFont;
            var textGo = ModUtils.GetChildAtPath("Global/Ui/UiMain/Constructor/Left/Scroll View/Viewport/Cont/FoldShipSettings/ShipSettings/ShipName/EditName/Static/Text");
            if (textGo != null)
            {
                var t = textGo.GetComponent<Text>();
                if (t != null && t.font != null)
                    _cachedFont = t.font;
            }
            return _cachedFont;
        }

        public static void Clear()
        {
            if (_foldRoot != null)
            {
                GameObject.Destroy(_foldRoot);
                _foldRoot = null;
            }
            _imageContainer = null;
            _shipImage = null;
            _armorText = null;
            _initialized = false;
            _gameSectionsSideRawImage = null;
            _gameSectionsTopRawImage = null;
            _zoneBoxes.Clear();

            // Clear calculations fold
            if (_calcFoldRoot != null)
            {
                GameObject.Destroy(_calcFoldRoot);
                _calcFoldRoot = null;
            }
            _penInputField = null;
            _penDisplayText = null;
            _calcInitialized = false;
            _selectedGunName = null;
            _referencePenetrationMm = 250f;
        }
    }
}
